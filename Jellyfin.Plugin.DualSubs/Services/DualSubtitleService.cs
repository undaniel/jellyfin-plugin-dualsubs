using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.DualSubs.Api;
using Jellyfin.Plugin.DualSubs.Configuration;
using Jellyfin.Plugin.DualSubs.Core;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DualSubs.Services;

/// <summary>
/// Orchestrates track selection, extraction, alignment, ASS generation and upload.
/// </summary>
public sealed class DualSubtitleService
{
    private const int RenderVersion = 1;

    private readonly ISubtitleManager _subtitleManager;
    private readonly SubtitleTextProvider _textProvider;
    private readonly ILocalizationManager _localizationManager;
    private readonly IProviderManager _providerManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<DualSubtitleService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DualSubtitleService"/> class.
    /// </summary>
    /// <param name="subtitleManager">Subtitle manager.</param>
    /// <param name="textProvider">Text provider.</param>
    /// <param name="localizationManager">Localization manager.</param>
    /// <param name="providerManager">Provider manager.</param>
    /// <param name="fileSystem">File system.</param>
    /// <param name="logger">Logger.</param>
    public DualSubtitleService(
        ISubtitleManager subtitleManager,
        SubtitleTextProvider textProvider,
        ILocalizationManager localizationManager,
        IProviderManager providerManager,
        IFileSystem fileSystem,
        ILogger<DualSubtitleService> logger)
    {
        _subtitleManager = subtitleManager;
        _textProvider = textProvider;
        _localizationManager = localizationManager;
        _providerManager = providerManager;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Generates every enabled pair for one item.
    /// </summary>
    /// <param name="video">Video item.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result per pair.</returns>
    public async Task<DualSubsGenerateResponse> GenerateAsync(Video video, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(video);

        var config = CurrentConfiguration;
        var style = BuildStyle(config);
        var results = new List<DualSubsPairResult>();
        var processed = new HashSet<string>(StringComparer.Ordinal);

        if (IsStrm(video))
        {
            return new DualSubsGenerateResponse(
                video.Id,
                (config.Pairs ?? [])
                .Where(pair => pair.Enabled)
                .Select(pair => new DualSubsPairResult(
                    $"{pair.PrimaryLanguage}+{pair.SecondaryLanguage}",
                    DualSubsStatus.Error,
                    null,
                    null,
                    0,
                    "STRM items are not supported."))
                .ToList());
        }

        foreach (var pair in (config.Pairs ?? []).Where(pair => pair.Enabled))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalized = NormalizePair(pair.PrimaryLanguage, pair.SecondaryLanguage);
            if (normalized is null)
            {
                results.Add(new DualSubsPairResult(
                    $"{pair.PrimaryLanguage}+{pair.SecondaryLanguage}",
                    DualSubsStatus.Error,
                    null,
                    null,
                    0,
                    "Primary and secondary languages must be different and valid."));
                continue;
            }

            if (!processed.Add(normalized.Key))
            {
                continue;
            }

            try
            {
                results.Add(await ProcessPairAsync(video, config, style, normalized, cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dual subtitles failed for item {ItemId} and pair {Pair}.", video.Id, normalized.Key);
                results.Add(new DualSubsPairResult(normalized.Key, DualSubsStatus.Error, null, null, 0, ex.Message));
            }
        }

        return new DualSubsGenerateResponse(video.Id, results);
    }

    /// <summary>
    /// Returns the tracks and offset that would be used, without writing anything.
    /// </summary>
    /// <param name="video">Video item.</param>
    /// <param name="pairQuery">Pair such as eng+spa.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Preview.</returns>
    public async Task<DualSubsPreviewResponse> PreviewAsync(Video video, string pairQuery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentException.ThrowIfNullOrWhiteSpace(pairQuery);

        if (IsStrm(video))
        {
            return new DualSubsPreviewResponse(
                video.Id,
                pairQuery,
                DualSubsStatus.Error,
                null,
                null,
                null,
                null,
                0,
                0,
                0,
                "STRM items are not supported.");
        }

        var parts = pairQuery.Split('+', 2, StringSplitOptions.TrimEntries);
        var normalized = parts.Length == 2 ? NormalizePair(parts[0], parts[1]) : null;
        if (normalized is null)
        {
            return new DualSubsPreviewResponse(
                video.Id,
                pairQuery,
                DualSubsStatus.Error,
                null,
                null,
                null,
                null,
                0,
                0,
                0,
                "Pair must look like eng+spa and use two different languages.");
        }

        var config = CurrentConfiguration;
        var style = BuildStyle(config);
        var source = PrimarySource(video);
        if (source is null)
        {
            return new DualSubsPreviewResponse(
                video.Id,
                normalized.Key,
                DualSubsStatus.Error,
                null,
                null,
                null,
                null,
                0,
                0,
                0,
                "No media source found.");
        }

        var selection = await SelectSourcesAsync(video, source, normalized, cancellationToken).ConfigureAwait(false);
        var offset = selection.Status == DualSubsStatus.Ok && style.AutoAlignOffset
            ? OffsetEstimator.Estimate(selection.PrimaryCues, selection.SecondaryCues, style.MaxOffsetSeconds)
            : 0;

        return new DualSubsPreviewResponse(
            video.Id,
            normalized.Key,
            selection.Status,
            selection.PrimaryStream?.Index,
            selection.SecondaryStream?.Index,
            selection.PrimaryStream?.DisplayTitle,
            selection.SecondaryStream?.DisplayTitle,
            selection.PrimaryCues.Count,
            selection.SecondaryCues.Count,
            offset,
            selection.Message);
    }

    /// <summary>
    /// Deletes every file generated by this plugin for one item.
    /// </summary>
    /// <param name="video">Video item.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of deleted files.</returns>
    public async Task<int> DeleteAllGeneratedAsync(Video video, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(video);

        var files = PluginFileDetector.FindForVideo(video);
        if (files.Count == 0)
        {
            return 0;
        }

        await DeleteFilesAsync(video, files, cancellationToken).ConfigureAwait(false);
        return files.Count;
    }

    private async Task<DualSubsPairResult> ProcessPairAsync(
        Video video,
        PluginConfiguration config,
        DualSubsStyleOptions style,
        LanguagePair pair,
        CancellationToken cancellationToken)
    {
        var source = PrimarySource(video);
        if (source is null || string.IsNullOrEmpty(source.Id))
        {
            return new DualSubsPairResult(pair.Key, DualSubsStatus.Error, null, null, 0, "No media source found.");
        }

        var selection = await SelectSourcesAsync(video, source, pair, cancellationToken).ConfigureAwait(false);
        if (selection.Status != DualSubsStatus.Ok)
        {
            return new DualSubsPairResult(
                pair.Key,
                selection.Status,
                selection.PrimaryStream?.Index,
                selection.SecondaryStream?.Index,
                0,
                selection.Message);
        }

        var offset = style.AutoAlignOffset
            ? OffsetEstimator.Estimate(selection.PrimaryCues, selection.SecondaryCues, style.MaxOffsetSeconds)
            : 0;
        var secondaryCues = OffsetEstimator.ApplyOffset(selection.SecondaryCues, offset);

        var configHash = ComputeConfigHash(pair.Key, style);
        var sourceHash = ComputeSourceHash(selection.PrimaryCues, selection.SecondaryCues, configHash);
        var existing = PluginFileDetector.FindForVideo(video)
            .Where(file => string.Equals(file.Pair, pair.Key, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (existing.Any(file => string.Equals(file.SourceHash, sourceHash, StringComparison.OrdinalIgnoreCase)))
        {
            return new DualSubsPairResult(
                pair.Key,
                DualSubsStatus.Unchanged,
                selection.PrimaryStream!.Index,
                selection.SecondaryStream!.Index,
                offset,
                null);
        }

        if (existing.Count > 0 && !config.OverwritePluginFiles)
        {
            return new DualSubsPairResult(
                pair.Key,
                DualSubsStatus.Unchanged,
                selection.PrimaryStream!.Index,
                selection.SecondaryStream!.Index,
                offset,
                "An existing plugin file has different sources, but OverwritePluginFiles is disabled.");
        }

        var replaced = existing.Count > 0;
        if (replaced)
        {
            await DeleteFilesAsync(video, existing, cancellationToken).ConfigureAwait(false);
        }

        var ass = AssWriter.Write(selection.PrimaryCues, secondaryCues, style, pair.Key, sourceHash, offset);
        var bytes = EncodingWithBom(ass);

        try
        {
            await using var memory = new MemoryStream(bytes, writable: false);
            await _subtitleManager.UploadSubtitle(
                video,
                new SubtitleResponse
                {
                    Format = "ass",
                    Language = pair.Primary,
                    IsForced = false,
                    IsHearingImpaired = false,
                    Stream = memory
                }).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission denied writing dual subtitles for item {ItemId} and pair {Pair}.", video.Id, pair.Key);
            return new DualSubsPairResult(
                pair.Key,
                DualSubsStatus.Error,
                selection.PrimaryStream!.Index,
                selection.SecondaryStream!.Index,
                offset,
                "Cannot write the subtitle file. Check folder permissions or disable 'Save subtitles with media' for this library.");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error writing dual subtitles for item {ItemId} and pair {Pair}.", video.Id, pair.Key);
            return new DualSubsPairResult(
                pair.Key,
                DualSubsStatus.Error,
                selection.PrimaryStream!.Index,
                selection.SecondaryStream!.Index,
                offset,
                "Cannot write the subtitle file. Check disk space, folder permissions or 'Save subtitles with media'.");
        }

        _providerManager.QueueRefresh(
            video.Id,
            new MetadataRefreshOptions(new DirectoryService(_fileSystem)),
            RefreshPriority.High);

        _logger.LogInformation(
            "Dual subtitles {Status} for item {ItemId} and pair {Pair} (offset {OffsetMs} ms).",
            replaced ? DualSubsStatus.Replaced : DualSubsStatus.Created,
            video.Id,
            pair.Key,
            offset);

        return new DualSubsPairResult(
            pair.Key,
            replaced ? DualSubsStatus.Replaced : DualSubsStatus.Created,
            selection.PrimaryStream!.Index,
            selection.SecondaryStream!.Index,
            offset,
            null);
    }

    private async Task<Selection> SelectSourcesAsync(
        Video video,
        MediaSourceInfo source,
        LanguagePair pair,
        CancellationToken cancellationToken)
    {
        var streams = source.MediaStreams ?? [];
        var isPluginFile = new Func<MediaStream, bool>(stream =>
        {
            if (!stream.IsExternal || stream.IsExternalUrl == true || string.IsNullOrWhiteSpace(stream.Path))
            {
                return false;
            }

            // A stale external path would make the encoder fail; skip it.
            return !File.Exists(stream.Path) || PluginFileDetector.TryRead(stream.Path) is not null;
        });

        var primaryCandidates = SourceTrackSelector.SelectCandidates(streams, pair.Primary, NormalizeLanguage, isPluginFile);
        var secondaryCandidates = SourceTrackSelector.SelectCandidates(streams, pair.Secondary, NormalizeLanguage, isPluginFile);

        if (primaryCandidates.Count == 0 || secondaryCandidates.Count == 0)
        {
            var bitmap = SourceTrackSelector.HasBitmapOnlyForLanguage(streams, pair.Primary, NormalizeLanguage)
                || SourceTrackSelector.HasBitmapOnlyForLanguage(streams, pair.Secondary, NormalizeLanguage);

            return Selection.Failure(
                bitmap ? DualSubsStatus.BitmapOnly : DualSubsStatus.MissingSource,
                bitmap
                    ? "A source language only has bitmap or forced subtitles."
                    : "One of the source languages has no usable text subtitles.");
        }

        var primaryFetched = await FetchCandidatesAsync(video, source.Id, primaryCandidates, cancellationToken).ConfigureAwait(false);
        var secondaryFetched = await FetchCandidatesAsync(video, source.Id, secondaryCandidates, cancellationToken).ConfigureAwait(false);

        if (primaryFetched.Count == 0 || secondaryFetched.Count == 0)
        {
            return Selection.Failure(DualSubsStatus.MissingSource, "A source track produced no cues after parsing.");
        }

        var primary = Choose(primaryFetched);
        var secondary = Choose(secondaryFetched);
        if (primary is null || secondary is null)
        {
            return Selection.Failure(DualSubsStatus.MissingSource, "A source track produced no cues after parsing.");
        }

        return new Selection(
            DualSubsStatus.Ok,
            null,
            primary.Stream,
            secondary.Stream,
            primary.Cues,
            secondary.Cues);
    }

    private async Task<List<FetchedCandidate>> FetchCandidatesAsync(
        Video video,
        string mediaSourceId,
        IReadOnlyList<MediaStream> candidates,
        CancellationToken cancellationToken)
    {
        var fetched = new List<FetchedCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var srt = await _textProvider.GetSrtAsync(video, mediaSourceId, candidate.Index, cancellationToken).ConfigureAwait(false);
                var cues = SrtParser.Parse(srt);
                if (cues.Count > 0)
                {
                    fetched.Add(new FetchedCandidate(candidate, cues));
                }
                else
                {
                    _logger.LogDebug(
                        "Subtitle stream {StreamIndex} of item {ItemId} produced no cues.",
                        candidate.Index,
                        video.Id);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Could not extract subtitle stream {StreamIndex} of item {ItemId}.",
                    candidate.Index,
                    video.Id);
            }
        }

        return fetched;
    }

    private static FetchedCandidate? Choose(List<FetchedCandidate> fetched)
    {
        var best = SourceTrackSelector.ChooseBest(
            fetched.Select(item => (item.Stream, item.Cues.Count)).ToList());

        return best is null ? null : fetched.First(item => ReferenceEquals(item.Stream, best));
    }

    private async Task DeleteFilesAsync(
        Video video,
        IReadOnlyList<PluginSubtitleFile> files,
        CancellationToken cancellationToken)
    {
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var stream in video.GetMediaStreams().Where(stream => stream.Type == MediaStreamType.Subtitle && stream.IsExternal))
        {
            if (!string.IsNullOrWhiteSpace(stream.Path))
            {
                indexes[Path.GetFullPath(stream.Path)] = stream.Index;
            }
        }

        var needsRefresh = false;
        foreach (var file in files
                     .OrderByDescending(file => indexes.GetValueOrDefault(Path.GetFullPath(file.Path), -1)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(file.Path);

            if (indexes.TryGetValue(path, out var index))
            {
                try
                {
                    await _subtitleManager.DeleteSubtitles(video, index).ConfigureAwait(false);
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete subtitle stream {Index} of item {ItemId} through Jellyfin.", index, video.Id);
                }
            }

            try
            {
                File.Delete(path);
                needsRefresh = true;
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not delete dual subtitle file {Path}.", path);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Permission denied deleting dual subtitle file {Path}.", path);
            }
        }

        if (needsRefresh)
        {
            await video.RefreshMetadata(cancellationToken).ConfigureAwait(false);
        }
    }

    private static MediaSourceInfo? PrimarySource(Video video)
    {
        return video.GetMediaSources(false).FirstOrDefault();
    }

    private static bool IsStrm(Video video)
    {
        return string.Equals(Path.GetExtension(video.Path), ".strm", StringComparison.OrdinalIgnoreCase);
    }

    private string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return string.Empty;
        }

        var info = _localizationManager.FindLanguageInfo(language);
        var normalized = info?.ThreeLetterISOLanguageName ?? info?.Name;
        return string.IsNullOrWhiteSpace(normalized)
            ? language.Trim().ToLowerInvariant()
            : normalized.ToLowerInvariant();
    }

    private LanguagePair? NormalizePair(string primary, string secondary)
    {
        var normalizedPrimary = NormalizeLanguage(primary);
        var normalizedSecondary = NormalizeLanguage(secondary);
        if (normalizedPrimary.Length == 0
            || normalizedSecondary.Length == 0
            || string.Equals(normalizedPrimary, normalizedSecondary, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new LanguagePair(normalizedPrimary, normalizedSecondary);
    }

    private static DualSubsStyleOptions BuildStyle(PluginConfiguration config)
    {
        var style = config.ToStyleOptions();
        var layout = string.Equals(style.Layout, "StackedBottom", StringComparison.OrdinalIgnoreCase)
            ? "StackedBottom"
            : "TopBottom";

        return style with
        {
            Layout = layout,
            PrimaryFontSize = Math.Clamp(style.PrimaryFontSize, 10, 200),
            SecondaryFontSize = Math.Clamp(style.SecondaryFontSize, 10, 200),
            OutlineSize = Math.Clamp(style.OutlineSize, 0, 20),
            MaxOffsetSeconds = Math.Clamp(style.MaxOffsetSeconds, 0, 120),
            PrimaryColor = SafeColor(style.PrimaryColor, "#FFFFFF"),
            SecondaryColor = SafeColor(style.SecondaryColor, "#FFE680")
        };
    }

    private static string SafeColor(string color, string fallback)
    {
        try
        {
            AssColor.FromHtml(color);
            return color;
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }

    private static string ComputeConfigHash(string pair, DualSubsStyleOptions style)
    {
        var json = JsonSerializer.Serialize(new { Version = RenderVersion, Pair = pair, Style = style });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static string ComputeSourceHash(
        IReadOnlyList<Cue> primaryCues,
        IReadOnlyList<Cue> secondaryCues,
        string configHash)
    {
        // Canonical cue text makes the hash independent of SRT numbering and line endings
        // produced by different extractor runs.
        var input = CanonicalCues(primaryCues) + "\n--\n" + CanonicalCues(secondaryCues) + configHash;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    private static string CanonicalCues(IReadOnlyList<Cue> cues)
    {
        return string.Join(
            '\n',
            cues.Select(cue => $"{cue.Start.Ticks}|{cue.End.Ticks}|{cue.Text}"));
    }

    private static byte[] EncodingWithBom(string text)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var preamble = encoding.GetPreamble();
        var body = encoding.GetBytes(text);
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    private static PluginConfiguration CurrentConfiguration => Plugin.Instance?.Configuration ?? new PluginConfiguration();

    private sealed record LanguagePair(string Primary, string Secondary)
    {
        public string Key => $"{Primary}+{Secondary}";
    }

    private sealed record FetchedCandidate(MediaStream Stream, IReadOnlyList<Cue> Cues);

    private sealed record Selection(
        string Status,
        string? Message,
        MediaStream? PrimaryStream,
        MediaStream? SecondaryStream,
        IReadOnlyList<Cue> PrimaryCues,
        IReadOnlyList<Cue> SecondaryCues)
    {
        public static Selection Failure(string status, string message)
        {
            return new Selection(status, message, null, null, [], []);
        }
    }
}

using System.Text;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DualSubs.Services;

/// <summary>
/// Extracts any text subtitle track as SRT through Jellyfin's subtitle encoder.
/// </summary>
public sealed class SubtitleTextProvider
{
    private readonly ISubtitleEncoder _subtitleEncoder;
    private readonly ILogger<SubtitleTextProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleTextProvider"/> class.
    /// </summary>
    /// <param name="subtitleEncoder">Subtitle encoder.</param>
    /// <param name="logger">Logger.</param>
    public SubtitleTextProvider(ISubtitleEncoder subtitleEncoder, ILogger<SubtitleTextProvider> logger)
    {
        _subtitleEncoder = subtitleEncoder;
        _logger = logger;
    }

    /// <summary>
    /// Gets the subtitle text as SRT with original timestamps.
    /// </summary>
    /// <param name="item">Video item.</param>
    /// <param name="mediaSourceId">Primary media source id.</param>
    /// <param name="streamIndex">Subtitle stream index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>SRT text.</returns>
    public async Task<string> GetSrtAsync(
        BaseItem item,
        string mediaSourceId,
        int streamIndex,
        CancellationToken cancellationToken)
    {
        await using var stream = await _subtitleEncoder.GetSubtitles(
            item,
            mediaSourceId,
            streamIndex,
            "srt",
            0,
            0,
            preserveOriginalTimestamps: true,
            cancellationToken).ConfigureAwait(false);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        if (text.Contains('\uFFFD', StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Subtitle stream {StreamIndex} of item {ItemId} contains replacement characters; the source encoding may be wrong.",
                streamIndex,
                item.Id);
        }

        return text;
    }
}

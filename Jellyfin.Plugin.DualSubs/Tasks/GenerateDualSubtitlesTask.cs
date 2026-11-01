using Jellyfin.Data.Enums;
using Jellyfin.Plugin.DualSubs.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DualSubs.Tasks;

/// <summary>
/// Generates dual subtitles for every video in the configured libraries.
/// </summary>
public sealed class GenerateDualSubtitlesTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly DualSubtitleService _service;
    private readonly ILogger<GenerateDualSubtitlesTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateDualSubtitlesTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="service">Dual subtitle service.</param>
    /// <param name="logger">Logger.</param>
    public GenerateDualSubtitlesTask(
        ILibraryManager libraryManager,
        DualSubtitleService service,
        ILogger<GenerateDualSubtitlesTask> logger)
    {
        _libraryManager = libraryManager;
        _service = service;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Generate dual subtitles";

    /// <inheritdoc />
    public string Key => "DualSubsGenerate";

    /// <inheritdoc />
    public string Description => "Creates dual-language ASS tracks from existing text subtitles.";

    /// <inheritdoc />
    public string Category => "Subtitles";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !(config.Pairs ?? []).Any(pair => pair.Enabled))
        {
            _logger.LogInformation("Dual subtitles task skipped: no enabled language pairs.");
            progress.Report(100);
            return;
        }

        var items = GetItems(config.LibraryIds ?? [])
            .Where(item => item is Video)
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .ToList();

        _logger.LogInformation("Dual subtitles task processing {Count} items.", items.Count);

        var created = 0;
        var errors = 0;
        for (var i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var response = await _service.GenerateAsync((Video)items[i], cancellationToken).ConfigureAwait(false);
                created += response.Results.Count(result => result.Status is Api.DualSubsStatus.Created or Api.DualSubsStatus.Replaced);
                errors += response.Results.Count(result => result.Status == Api.DualSubsStatus.Error);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Dual subtitles task failed for item {ItemId}.", items[i].Id);
            }

            progress.Report((i + 1) * 100.0 / items.Count);
        }

        progress.Report(100);
        _logger.LogInformation(
            "Dual subtitles task finished: {Count} items, {Created} files created or replaced, {Errors} pair errors.",
            items.Count,
            created,
            errors);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = TimeSpan.FromHours(4).Ticks
            }
        ];
    }

    private IReadOnlyList<BaseItem> GetItems(IReadOnlyCollection<Guid> libraryIds)
    {
        if (libraryIds.Count == 0)
        {
            return _libraryManager.GetItemList(CreateQuery(null));
        }

        var items = new List<BaseItem>();
        foreach (var libraryId in libraryIds)
        {
            items.AddRange(_libraryManager.GetItemList(CreateQuery([libraryId])));
        }

        return items;
    }

    private static InternalItemsQuery CreateQuery(Guid[]? ancestorIds)
    {
        return new InternalItemsQuery
        {
            Recursive = true,
            IsVirtualItem = false,
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Episode, BaseItemKind.MusicVideo, BaseItemKind.Video],
            MediaTypes = [MediaType.Video],
            EnableTotalRecordCount = false,
            AncestorIds = ancestorIds ?? []
        };
    }
}

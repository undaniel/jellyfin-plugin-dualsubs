using System.Collections.Concurrent;
using Jellyfin.Plugin.DualSubs.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DualSubs.EventHandlers;

/// <summary>
/// Debounces library events and generates dual subtitles in the background.
/// </summary>
public sealed class ItemAddedHandler : BackgroundService
{
    private readonly ILibraryManager _libraryManager;
    private readonly DualSubtitleService _service;
    private readonly ILogger<ItemAddedHandler> _logger;
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _pending = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemAddedHandler"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="service">Dual subtitle service.</param>
    /// <param name="logger">Logger.</param>
    public ItemAddedHandler(
        ILibraryManager libraryManager,
        DualSubtitleService service,
        ILogger<ItemAddedHandler> logger)
    {
        _libraryManager = libraryManager;
        _service = service;
        _logger = logger;
    }

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemChanged;
        _libraryManager.ItemUpdated += OnItemChanged;
        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemChanged;
        _libraryManager.ItemUpdated -= OnItemChanged;
        return base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueItemsAsync(stoppingToken).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dual subtitles event handler loop failed.");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private void OnItemChanged(object? sender, ItemChangeEventArgs args)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.ProcessOnItemAdded)
        {
            return;
        }

        if (args.Item is not Video video
            || string.Equals(Path.GetExtension(video.Path), ".strm", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _pending[video.Id] = DateTimeOffset.UtcNow;
    }

    private async Task ProcessDueItemsAsync(CancellationToken cancellationToken)
    {
        if (_pending.IsEmpty)
        {
            return;
        }

        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.ProcessOnItemAdded)
        {
            _pending.Clear();
            return;
        }

        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(Math.Clamp(config.DebounceSeconds, 5, 3600));
        var due = _pending
            .Where(entry => entry.Value <= cutoff)
            .Select(entry => entry.Key)
            .Take(200)
            .ToArray();

        if (due.Length == 0)
        {
            return;
        }

        foreach (var itemId in due)
        {
            _pending.TryRemove(itemId, out _);
        }

        var maxConcurrency = Math.Clamp(config.MaxConcurrency, 1, 4);
        using var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        await Task.WhenAll(due.Select(async itemId =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var item = _libraryManager.GetItemById<BaseItem>(itemId);
                if (item is Video video)
                {
                    await _service.GenerateAsync(video, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automatic dual subtitle generation failed for item {ItemId}.", itemId);
            }
            finally
            {
                gate.Release();
            }
        })).ConfigureAwait(false);
    }
}

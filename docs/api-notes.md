# Jellyfin 12.1 API notes

Verified against `Jellyfin.Controller` / `Jellyfin.Model` **12.1.0** and the
`jellyfin/jellyfin` tag **v12.1** on 2026-11-01.

## `ISubtitleEncoder.GetSubtitles`

Source: `MediaBrowser.Controller/MediaEncoding/ISubtitleEncoder.cs`

```csharp
Task<Stream> GetSubtitles(
    BaseItem item,
    string mediaSourceId,
    int subtitleStreamIndex,
    string outputFormat,
    long startTimeTicks,
    long endTimeTicks,
    bool preserveOriginalTimestamps,
    CancellationToken cancellationToken);
```

The plugin calls it with `outputFormat = "srt"`, `startTimeTicks = 0`,
`endTimeTicks = 0` (the same value used by `SubtitleController` for a full file) and
`preserveOriginalTimestamps = true`.

## `ISubtitleManager.UploadSubtitle`

Source: `MediaBrowser.Controller/Subtitles/ISubtitleManager.cs`

```csharp
Task UploadSubtitle(Video video, SubtitleResponse response);

public class SubtitleResponse
{
    public string Language { get; set; }
    public string Format { get; set; }
    public bool IsForced { get; set; }
    public bool IsHearingImpaired { get; set; }
    public Stream Stream { get; set; }
}
```

The request has no `CancellationToken` in 12.1.

Important implementation detail from `MediaBrowser.Providers/Subtitles/SubtitleManager.cs`:

- The target name is `Path.GetFileNameWithoutExtension(video.Path) + "." + language`
  plus `.forced` / `.sdh`, then the format extension.
- The file is saved in the media folder when `LibraryOptions.SaveSubtitlesWithMedia` is
  true, otherwise in `video.GetInternalMetadataPath()`.
- If the target name exists, Jellyfin appends `.0`, `.1`, ... and **never overwrites**.
- The native endpoint `POST /Videos/{itemId}/Subtitles` queues a high-priority metadata
  refresh after the upload. This plugin does the same through `IProviderManager.QueueRefresh`.

Because the title cannot be supplied, the visible client title comes from the filename
and its language suffix (for example "English"), not "Dual ENG/SPA". This limitation is
documented in the README instead of reimplementing file saving.

## `ISubtitleManager.DeleteSubtitles`

```csharp
Task DeleteSubtitles(BaseItem item, int index);
```

It resolves the stream by index from `IMediaSourceManager`, deletes the file and refreshes
metadata. The plugin uses it when the indexed stream path matches the generated file, and
falls back to deleting the marked file directly when the item has not been refreshed yet.

## Language normalization

Source: `MediaBrowser.Model/Globalization/ILocalizationManager.cs`

```csharp
CultureDto? FindLanguageInfo(string language);
```

`FindLanguageInfo` accepts display names, two-letter and three-letter codes (and the
culture's alternate three-letter names). The plugin stores the lower-case
`CultureDto.ThreeLetterISOLanguageName`, falling back to the input lower-cased.

## `ILibraryManager` events

```csharp
event EventHandler<ItemChangeEventArgs> ItemAdded;
event EventHandler<ItemChangeEventArgs> ItemUpdated;
event EventHandler<ItemChangeEventArgs> ItemRemoved;
```

`ItemChangeEventArgs.Item` is the changed `BaseItem`.

## `IScheduledTask`

```csharp
string Name { get; }
string Key { get; }
string Description { get; }
string Category { get; }
Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken);
IEnumerable<TaskTriggerInfo> GetDefaultTriggers();
```

Jellyfin discovers `IScheduledTask` implementations in plugin assemblies through
`ApplicationHost.GetExports<IScheduledTask>()`; no manual DI registration is needed.

## External subtitle naming

Source: `Emby.Naming/ExternalFiles/ExternalPathParser.cs`

The filename suffix is parsed for the language, default/forced and hearing-impaired flags,
and any remaining suffix becomes the track title. `UploadSubtitle` only controls the
language part, so this plugin cannot add a custom title through the native API.

## Source hash

`DualSubs-SourceHash` is `SHA-256(canonicalPrimaryCues + "\n--\n" +
canonicalSecondaryCues + configHash)`. A canonical cue is
`start.Ticks|end.Ticks|text`. The raw SRT text is not used because Jellyfin's encoder can
format the same cue differently between runs, which broke idempotency in integration
testing. `configHash` includes a render version plus the style and alignment settings.

## Stream indexes and Jellyfin's subtitle cache

`ISubtitleEncoder` extracts embedded subtitles with
`ExtractAllExtractableSubtitles`, which writes one file per subtitle stream to
`PathManager.GetSubtitleFolderPath(mediaSourceId)` named after **the stream index**.

Adding or removing an external subtitle shifts every later index, but the cached files keep
their old names. The encoder then serves the previous track for that index. Verified on
Jellyfin 12.4: after a generated file was added at index 0, asking for index 4 (English)
returned Danish, index 7 (Spanish) returned French, and index 12 (Norwegian) returned
Polish — exactly the pre-change table.

The service therefore deletes that cache folder after it adds or removes a file. Jellyfin
regenerates it on demand. Deleting it on every run would force a full ffmpeg extraction per
item, so it is only invalidated when the index table actually changes.

## Deleting generated files

`ISubtitleManager.DeleteSubtitles(item, index)` resolves the index internally. Because of
the shift described above it can point at a different track, so the service deletes its own
files by path and queues a metadata refresh instead.

## Admin policy

`MediaBrowser.Common.Api.Policies.RequiresElevation` is the policy used by the native
subtitle management endpoints. The plugin controller applies it to all three routes.

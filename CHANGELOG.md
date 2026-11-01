# Changelog

All notable changes to this project are documented here.

## 1.0.2.0 - 2026-11-01

- Fix: after the first generated file was added, later runs could extract a different
  language (for example Danish instead of English) and write it into the dual track.
  Jellyfin caches extracted embedded subtitles by stream index in
  `PathManager.GetSubtitleFolderPath`; a new external subtitle shifts those indexes but the
  cache files keep their old names. The plugin now clears that cache for the item whenever
  it adds or removes a file.
- Fix: generated files are deleted by path instead of `ISubtitleManager.DeleteSubtitles`,
  which resolves the index against the same shifted table and could delete a user's track.
- Note: an item processed by 1.0.1.0 may need two runs to converge, because the first run
  still reads the stale cache and only then clears it.

## 1.0.1.0 - 2026-11-01

- Fix: after a generated file was added, the next run could pick the wrong source tracks
  (for example Danish and French instead of English and Spanish) because stream indexes
  were resolved against a stale media source table. Selection now uses
  `IMediaSourceManager.GetPlaybackMediaSources`, the same table the subtitle encoder uses.
- Fix: generated files are deleted by path instead of `ISubtitleManager.DeleteSubtitles`,
  which resolved the index against that same shifted table and could delete a user's
  subtitle track.

## 1.0.0.0 - 2026-11-01

- Initial release for Jellyfin 12.x / .NET 10.
- Global language pairs with ISO 639-1 and 639-2 codes.
- Source selection: text tracks only, forced tracks ignored, non-SDH and external tracks preferred.
- ASS output with TopBottom and StackedBottom layouts, configurable sizes, colors and italic.
- Automatic constant-offset estimation, applied to the secondary track only.
- Idempotent generation, ownership marker and `SourceHash` regeneration.
- Manual REST API: generate, delete generated files, preview source tracks and offset.
- Scheduled task "Generate dual subtitles" with progress reporting.
- Debounced automatic processing of added and updated items.
- Configuration page with pair editor, library selection and static preview.
- Unit tests for parser, ASS writer, colors, offset estimator, source selection and file marker.

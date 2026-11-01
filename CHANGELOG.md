# Changelog

All notable changes to this project are documented here.

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

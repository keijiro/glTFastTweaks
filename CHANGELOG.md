# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.1.2] - 2026-09-16

### Fixed

- Fixed compile errors with glTFast 7.0's assembly and API renames.

### Changed

- Referenced the glTFast assembly by GUID to survive the rename.
- Raised the glTFast dependency to 6.20.0.

## [1.1.1] - 2026-06-21

### Added

- Show each asset's directory path next to its file name.

### Changed

- Narrowed the Size, Compression, and Filter columns.

### Removed

- Removed console logging, the empty-state hint, and per-row tooltips.

## [1.1.0] - 2026-06-20

### Added

- Added a "Scan for glTF Assets" button to rebuild the asset list on demand.

### Changed

- Built the asset list from a full project scan instead of recording imports.
- Removed entries whose asset no longer exists during a scan.
- Disabled texture overrides by default.

## [1.0.0] - 2026-06-20

### Added

- Initial release.

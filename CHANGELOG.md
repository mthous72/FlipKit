# Changelog

All notable changes to CardLister will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added
- **Tailscale Sync** - Multi-computer card access via private Tailscale network
  - Simple REST API sync server (`CardLister.Api`) runs on main computer
  - Automatic synchronization on startup and exit (optional)
  - Manual "Sync Now" button in Settings
  - Timestamp-based conflict resolution (newest UpdatedAt wins)
  - Real-time sync status feedback with color-coded messages
  - Zero cost solution - no cloud hosting required
  - Secure and private - data stays on Tailscale network
  - Fast local network speeds even when remote
  - Perfect for laptop + desktop workflows
- **Quick Edit Panel** - Inline editing in inventory view
  - Side panel opens without navigation
  - Preserves scroll position and selection
  - "Full Edit" button to open comprehensive edit view when needed
- **Full-Resolution Images** - Edit view now displays full-size images
  - Prioritizes ImgBB URLs (used for Whatnot) over local files
  - Automatic download from hosted URLs
  - Better card detail visibility for verification

### Fixed
- Entity Framework tracking conflicts when saving quick edits from inventory view
- Detached already-tracked entities before updating to prevent database errors

## [2.0.4] - 2025-01-XX

### Fixed
- JSON parsing errors during mobile scanning - improved error handling for malformed AI responses
- UI improvements and error messaging

## [2.0.3] - 2025-01-XX

### Fixed
- Mobile scanning errors and UI improvements

## [2.0.2] - 2025-01-XX

### Added
- Build script version bump

## Previous Releases

See git history for earlier changes. The application has been in active development with the following major milestones:

- **v2.1.0** - Web application release with mobile scanning
- **v2.0.x** - Desktop application stabilization and bug fixes
- **v1.x** - Initial desktop application development

---

## Release Notes Format

### Added
New features that were added

### Changed
Changes to existing functionality

### Deprecated
Features that will be removed in upcoming releases

### Removed
Features that were removed

### Fixed
Bug fixes

### Security
Security improvements or vulnerability fixes

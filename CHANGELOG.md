# Changelog

All notable changes to FlipKit will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

_Nothing yet._

## [3.6.1] - 2026-05

### Added
- **Privacy & Data Handling section in Settings** — new section (Desktop and Web) lists every external service (OpenRouter, Ximilar, ImgBB, eBay API, eBay deeplinks), exactly what data is sent, and when it is triggered. Includes a "no telemetry" callout confirming FlipKit sends nothing automatically.
- **First-run AI scan consent dialog** — before the first AI scan in a new installation, FlipKit shows a one-time dialog explaining that card images will be sent to OpenRouter/Ximilar. A "Remember my choice" checkbox suppresses future prompts. Available in both Desktop (Avalonia modal) and Web (inline banner with AntiForgeryToken form).
- **Secrets encrypted at rest** — all API keys (OpenRouter, ImgBB, Ximilar, eBay Client ID/Secret, eBay OAuth tokens) are now stored as `protected:<ciphertext>` in `config.json` using `Microsoft.AspNetCore.DataProtection` (DPAPI on Windows, AES-256-GCM file key ring on Linux/macOS). Existing plaintext keys are transparently migrated to encrypted form on the next save. Desktop and Web share the same key ring (`%LOCALAPPDATA%\FlipKit\DataProtection-Keys`) and can decrypt each other's values.

### Changed
- **Expanded README disclaimer** — added explicit paragraphs covering AI accuracy risk (AI output is probabilistic, every result must be verified), financial-decision risk (pricing data is reference only, not investment advice), no professional advice, and use-at-your-own-risk statement.

## [3.6.0] - 2026-05

### Added
- **Direct eBay listing creation via Sell Inventory API** — the new "Publish to eBay" page (Desktop) selects priced cards with images and publishes them directly to eBay using the Sell Inventory REST API. Each card becomes an inventory item + offer, then is published to a live listing whose ID is stored on the card.
- **eBay OAuth Authorization Code flow in Settings** — RuName field, "Connect eBay Account" button (opens browser to eBay's auth page), and a paste-back panel that accepts either a raw authorization code or the full redirect URL from the browser's address bar. "Fetch Account Policies" loads the seller's fulfillment, payment, and return policies for use in offers.

### Removed
- **eBay CSV export** — eBay sunsetted the File Exchange CSV bulk-upload pipeline, so the eBay option has been removed from the Export page (Desktop and Web). The `ExportPlatform.eBay` enum value is retained to avoid serialization breaks. Whatnot/COMC/Generic CSV exports are unchanged.

## [3.5.0] - 2026-05

### Added
- **eBay API credentials in Settings** — Client ID (App ID) and Client Secret fields added to the Settings page in both Desktop and Web. Credentials are stored in `config.json` alongside other API keys and validated against the eBay OAuth token endpoint via the **Test** button. Prepares the app for direct eBay listing creation via the Sell Inventory API.

## [3.4.0] - 2026-05

### Added
- **Save Draft from Scan page** — capture card photos and save as a sequentially-named draft (Draft 1, Draft 2, …) before or after an AI scan attempt. Photos are immediately uploaded to ImgBB so the draft is accessible remotely via Tailscale/API. The scan page retains the loaded images after saving so you can retry the scan without re-uploading, or click Clear to move to the next card. Available in both Desktop and Web.
- **Model catalog in Web Settings** — the Settings page (Docker/remote mode) now shows available OpenRouter free and paid vision models alongside API key management, matching the Desktop scan model selector.

## [3.3.6] - 2026-05

### Notes
- Current shipping release. Headline 3.x features: Hub unification (Desktop + embedded Web/API), Tailscale sync, eBay Bulk CSV export, Quick Edit Panel, full-resolution images, paid-model consent, live OpenRouter catalog.

### v3.x history
- Per-version 3.x notes are intentionally not duplicated here. To recover the full pre-3.3.6 history, read the original `release-notes-v3.x.md` files via git: `git show b540c67^:release-notes-v3.0.0.md` (and `v3.1.0.md`). They were deleted in commit `b540c67` (Phase 2 doc tidy).
- The CardLister → FlipKit rebrand record lives at [Docs/archive/REBRAND-COMPLETION-SUMMARY.md](Docs/archive/REBRAND-COMPLETION-SUMMARY.md).

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

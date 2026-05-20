# ADR-001: Hub architecture (Desktop + embedded Web/API servers) over separate apps

**Status:** Accepted
**Date:** 2026-05-04 (decision made earlier; codified during refactor Phase 6)
**Related:** [HUB-ARCHITECTURE.md](../HUB-ARCHITECTURE.md), refactor Phase 6 (re-baseline)

## Context

FlipKit started as a single-project Avalonia desktop app. The Web app was added later for mobile access (phone/tablet browsers via Tailscale), and the Api server was added so the Desktop and Web could consume data remotely without each opening its own SQLite connection over a network share.

That left three deployment options on the table:

1. **Three separate apps**, distributed independently. User installs whichever they need.
2. **Hub:** one bundle (Desktop) that owns the lifecycle of the Web and Api servers, started/stopped from a Settings UI.
3. **Web-first:** drop the Desktop app, deliver everything as the browser app.

We chose option 2.

## Decision

The Desktop app is the **single user-installed artifact**. It manages the Web and Api as child processes via `ServerManagementService` and exposes Start/Stop controls in Settings. Users get one install, one set of settings, one database. The Web and Api binaries ship inside the Desktop bundle.

`build-release.ps1` produces `FlipKit-Hub-Windows-x64-vX.Y.Z.zip` and `FlipKit-Hub-Linux-x64-vX.Y.Z.zip` — both contain Desktop + Web + Api together.

## Consequences

**Positive:**
- One installer to maintain, one update story.
- The Desktop app is the source of truth for paths, env vars, and lifecycle. No "did I start the API server?" foot-guns.
- All three projects share the same `%LocalAppData%/FlipKit/cards.db` automatically — no path-config drift.
- For users who never need remote access: the Web and Api processes simply never start. Zero overhead.

**Negative:**
- Server lifecycle code (start/stop/health check) lives in `ServerManagementService` and adds complexity to the Desktop SettingsViewModel. The Phase 5a fix for the §7.10 race conditions is a direct consequence of this coupling.
- Headless deployments (e.g. running just the Api on a NAS) are awkward — you have to launch the Desktop app to start the Api, or run the Api `.exe` directly and skip Hub.
- Bundle size is larger than any single app would be. Acceptable for a desktop install.

**Neutral:**
- Web-first was rejected because: native file dialogs, native drag-drop, and AI-vision UX all want a real desktop app. Three-separate-apps was rejected because: the multi-install matrix and config drift were the pain that drove this decision in the first place.

## Status

Accepted. No plans to revisit unless the Web app's feature set fully overtakes the Desktop's (which would invalidate the "Desktop is the primary surface" assumption).

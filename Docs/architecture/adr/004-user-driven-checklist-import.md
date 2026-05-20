# ADR-004: User-driven Checklist Insider import (legal posture)

**Status:** Accepted
**Date:** 2026-05-04
**Related:** [17-FUTURE-ROADMAP.md #1](../../planning/roadmap.md), [28-CHECKLIST-INSIDER-IMPORT-PLAN.md](../../planning/checklist-insider-import-plan.md)

## Context

Roadmap item #1 is to populate `SetChecklist` data for modern card releases automatically — without that, variation verification falls back to whatever's pre-seeded, and most current-year sets aren't.

The obvious sources are [checklistinsider.com](https://www.checklistinsider.com/), [TCDB](https://www.tcdb.com/), and [Beckett](https://www.beckett.com/). All three have the data, all three have terms of use that **forbid commercial scraping or mirroring** of their checklist data. Two paths exist:

1. **Server-side scrape:** FlipKit servers fetch and parse checklist pages on the user's behalf. Operates as a commercial product against the host site.
2. **User-driven import:** the user downloads a checklist file from their own browser session under the site's individual personal-use license, then opens that file in FlipKit.

We chose path 2.

## Decision

The "Checklist Insider" feature ships as:
- A file picker that accepts `.xlsx` files from the user's filesystem.
- A `ClosedXML`-based `ExcelChecklistImporter` that parses the file into `SetChecklist` + `ChecklistCard` entities.
- A "Get Checklist for this set" deeplink in scan results that opens checklistinsider.com in the user's browser to the relevant set page.

FlipKit **never** issues an HTTP request against checklistinsider.com's servers. No scraping, no mirroring, no caching their HTML, no automated crawling. The user does the download themselves under whatever personal-use license the site grants individuals.

The same approach applies to TCDB / Beckett if either ever offers a user-downloadable export. Until then, this feature is single-source.

## Why

- **Compliance with the source's ToU.** Personal-use download licenses cover what individual users do in their own browser. They don't extend to a product fetching on their behalf.
- **Same legal posture as any app that opens a user-supplied file.** Excel opens .xlsx files from disk; that doesn't make Microsoft liable for whatever the file contains. FlipKit's a parser of user-supplied files, full stop.
- **Forces the user to be the consenting party.** The download is their action, not ours. If a site's ToU changes, users can stop using the source and the feature still works for whatever they've already downloaded.

## Consequences

**Positive:**
- Clean legal posture. No "did we just scrape something?" questions.
- Works offline once the file's downloaded. No service dependency at runtime.
- Cheap to ship — a parser + a file dialog. No server-side fetcher, no rate-limiting, no User-Agent rotation, no anti-bot dance.
- Generalizes to any future source that offers user-downloadable exports. The same `IExcelChecklistImporter` interface can pick up TCDB/Beckett if they ever publish an export.

**Negative:**
- More user friction than a "just works" automated fetcher. User has to find the right page, download the file, then import it in FlipKit. The "Get Checklist for this set" deeplink helps but doesn't eliminate the round trip.
- If checklistinsider.com changes their `.xlsx` format, our parser breaks. The fix is local code, but users may hit the failure before we ship the fix.
- We can't bulk-prepopulate every release — only sets the user has actually imported get the variation-verification benefit.

## Alternatives considered

- **Pay for licensed access to a checklist database.** None offered at a reseller-friendly price point at the time of decision.
- **Crowd-sourced user uploads.** Punted — would need a moderation system and a hosting story we don't have. Could be revisited once the user-driven import is shipping and there's signal that users want to share imports.
- **Hand-curate checklists ourselves.** Considered for the top ~20 modern releases. Rejected as a maintenance burden that scales linearly with calendar time.

## Status

Accepted. The feature itself is on the roadmap (Item #1) but not yet shipped. This ADR captures the constraint that any implementation must respect.

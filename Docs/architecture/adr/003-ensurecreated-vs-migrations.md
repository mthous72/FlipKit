# ADR-003: `EnsureCreated` + `SchemaUpdater` over EF migrations

**Status:** Accepted (with planned migration path)
**Date:** 2026-05-04
**Related:** [`FlipKit.Core/Data/SchemaUpdater.cs`](../../../FlipKit.Core/Data/SchemaUpdater.cs), [`FlipKit.Desktop/App.axaml.cs:171`](../../../FlipKit.Desktop/App.axaml.cs), [`FlipKit.Web/Program.cs:110`](../../../FlipKit.Web/Program.cs), refactor plan §7.6

## Context

EF Core ships two ways to set up a SQLite database:
- **`Database.EnsureCreated()`** — creates the schema from the current model. No migration history, no rollback. Cheap to run, idempotent, but can't evolve a live database when the model changes.
- **`Database.Migrate()`** — applies a sequence of migration scripts that are checked in to source control. Tracks the current schema version in a `__EFMigrationsHistory` table.

FlipKit started with `EnsureCreated()` because the schema was simple and changing fast. Once users started shipping with installed copies of `cards.db`, schema changes had to land somewhere. Two paths existed:
1. Switch wholesale to migrations and write a one-off script to backfill `__EFMigrationsHistory` for every existing user database.
2. Keep `EnsureCreated()` as the "fresh install" path and add a runtime `SchemaUpdater` that checks for missing columns/tables and `ALTER TABLE`s them in.

We took path 2.

## Decision

Both surfaces stand:
- **`Database.EnsureCreated()`** is called at startup in Desktop ([`App.axaml.cs:171`](../../../FlipKit.Desktop/App.axaml.cs)) and Web ([`Program.cs:110`](../../../FlipKit.Web/Program.cs)). Creates the database fresh on first install with the current model.
- **`SchemaUpdater`** ([`FlipKit.Core/Data/SchemaUpdater.cs`](../../../FlipKit.Core/Data/SchemaUpdater.cs)) runs after `EnsureCreated` and applies additive `ALTER TABLE IF NOT EXISTS` style fixups for columns added since prior versions. It's an accreting list of columns — every new optional column gets an entry here so that upgrading users get the column without losing data.

New columns get added in two places: as a property on the model (so EF knows about it for queries) and as a row in `SchemaUpdater` (so existing user databases pick it up). Both are required.

## Why this over real migrations

- **No story for backfilling `__EFMigrationsHistory` retroactively.** Every user database that exists today was created by `EnsureCreated`. Switching to `Migrate` cleanly would require writing a startup detector that distinguishes "fresh install" from "needs backfill" and either runs `EnsureCreated` once + stamps the history, or runs `Migrate` against an empty history. That's a non-trivial amount of code that runs on every startup forever.
- **SQLite makes `ALTER TABLE ADD COLUMN` cheap and safe.** We're not doing complex schema transformations (renames, drops, type changes). Every change has been "add a nullable column."
- **Migrations on SQLite have known sharp edges:** SQLite can't drop columns or change types directly; EF Core works around this by recreating the table, which is risky on user databases with WAL files in flight.

## Consequences

**Positive:**
- Zero-friction schema additions. Add the property, add the SchemaUpdater entry, ship.
- Existing users never need to re-create their database.
- `SchemaUpdater` is testable directly (it's just SQL) and trivial to read.

**Negative:**
- `SchemaUpdater` accretes forever. Currently ~175 lines. Every column decision is fossilized.
- We can't rename or drop columns cleanly. To remove a column we'd have to add migration support (or live with the dead column).
- New contributors have to learn the dual pattern. Easy to forget the SchemaUpdater entry — the tests catch it via fresh-DB creation, but only if a test exercises the new column.
- Refactoring the database (e.g. splitting a table) is not feasible without first migrating to real EF migrations.

## Future

Plan §7.6 has this earmarked: when a non-additive schema change becomes necessary (rename, type change, table split), that's the trigger to invest in real EF migrations + a one-time `__EFMigrationsHistory` backfill. Until then, the cost of keeping `SchemaUpdater` is small.

The Phase 4.5 D3 fix (`ValueComparer<List<T>>` on `SetChecklist.Cards` and `KnownVariations`) is unrelated to this ADR but worth flagging as the kind of EF-Core gotcha that the dual approach hides — `EnsureCreated` doesn't expose the missing comparer at startup, only the silent mutation-tracking failure at runtime. Real migrations wouldn't have caught it either.

## Status

Accepted. Re-evaluate when a non-additive schema change is on the roadmap.

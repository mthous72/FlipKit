# Architecture Decision Records

These ADRs capture the **non-obvious choices** that shaped FlipKit Hub. They exist so that future contributors (and future-you) can ask "why is it built this way?" and get a real answer instead of guessing from the code.

Format is loose — each ADR has Context, Decision, Consequences, and Status. Short and direct, no Michael-Nygard ceremony.

| ID | Title | Status |
|---|---|---|
| [ADR-001](001-hub-architecture.md) | Hub (Desktop + embedded servers) over separate apps | Accepted |
| [ADR-002](002-net8-net9-mix.md) | net8.0 + net9.0 framework mix | Accepted (transitional) |
| [ADR-003](003-ensurecreated-vs-migrations.md) | `EnsureCreated` + `SchemaUpdater` over EF migrations | Accepted (with planned migration path) |
| [ADR-004](004-user-driven-checklist-import.md) | User-driven Checklist Insider import (legal posture) | Accepted |
| [ADR-005](005-avalonia-over-maui-wpf.md) | Avalonia UI over MAUI / WPF | Accepted |

Add new ADRs by copying the format and incrementing the ID. Don't rewrite an accepted ADR — write a new one that supersedes it and update the old one's Status to "Superseded by ADR-NNN".

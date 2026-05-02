# FlipKit Future Roadmap

## Document Purpose

This document outlines planned future enhancements, architectural improvements, and long-term vision for FlipKit. As of February 2026, the MVP is ~80-90% complete with full end-to-end functionality. This roadmap guides continued development.

---

## Current Status Summary

**✅ What Works Today:**
- AI-powered card scanning (11 free vision models)
- Variation verification with checklist database
- Single-card and bulk scanning workflows
- Inventory management with filtering and search
- Pricing research via browser integration
- Whatnot CSV export with image hosting
- Sales tracking and financial reporting
- Graded card support (PSA, BGS, CGC, etc.)
- Checklist learning and management
- Setup wizard and settings

**🚧 In Progress:**
- Bulk scanning feature (feature/bulk-scan branch)

**📋 This Document:**
- What we're planning to build next
- Priority levels and rationale
- Technical approach considerations

---

## High Priority (Next 3-6 Months)

### 0. User-Driven Checklist Excel Import (Checklist Insider)

**Status:** 📋 Planned
**Priority:** High
**Effort:** Medium (2-3 weeks)
**Plan Doc:** [28-CHECKLIST-INSIDER-IMPORT-PLAN.md](28-CHECKLIST-INSIDER-IMPORT-PLAN.md)

**Description:** Let users populate `SetChecklist` by downloading per-set Excel files from [checklistinsider.com](https://www.checklistinsider.com/) themselves and importing the .xlsx into FlipKit via a file picker. Closes the gap where most modern releases aren't pre-seeded in the bundled checklist DB.

**Why user-driven (not automated):** Checklist Insider's ToU forbids commercial scraping/mirroring but grants individual users a personal-use download license. By having FlipKit only ship a parser (ClosedXML) and a UI — never touching their site — we stay clean of ToU and rate-limit issues entirely. Same legal posture as any app that opens a user-supplied file.

**What it adds:**
- "Import Checklist" view (Desktop + Web) with file picker, parse-preview, edit metadata, commit
- ClosedXML-based `ExcelChecklistImporter` in FlipKit.Core that walks the all-caps subset-header rows used by Checklist Insider's Able2Extract-generated files
- New fields on `ChecklistCard`: `IsAutograph`, `IsParallel`, `IsInsert` (derived from subset name)
- New fields on `SetChecklist`: `DataSource`, `ImportedAt` (provenance tracking)
- "Get Checklist for this set" deeplink in scan results when no checklist is imported yet

**Phase 2 follow-ups:** PDF odds-sheet importer (PdfPig) for parallels/print-runs/signers, batch folder import, manufacturer dealer-kit PDF support.

### 1. Complete Bulk Scanning Feature

**Status:** 🚧 In Progress (feature/bulk-scan)

**Description:** Multi-card batch scanning workflow with front/back pairing, progress tracking, and rate-limit handling.

**What's Left:**
- Finalize UI polish
- Enhanced error handling for failed scans
- Batch save with validation
- Testing with 50+ card batches

**Success Criteria:**
- Can scan 50+ cards in one session
- Front/back pairing works reliably
- Progress can be paused and resumed
- Rate limits don't break the workflow

### 2. Three-Project Architecture Refactor

**Priority:** High
**Effort:** Medium (2-3 weeks)
**Benefits:** Better testability, cleaner separation of concerns

**Current:** Single FlipKit.csproj with all code
**Target:** Three projects with clean boundaries

**New Structure:**
```
FlipKit.App/           # Avalonia UI layer
├── Views/               # XAML views
├── Converters/          # Value converters
├── Styles/              # Themes and styles
├── Assets/              # Images, icons
├── App.axaml.cs         # DI setup, startup
└── ViewLocator.cs       # ViewModel → View mapping

FlipKit.Core/         # Business logic (no UI refs)
├── ViewModels/          # All ViewModels
├── Models/              # Domain entities
├── Services/            # Service interfaces only
└── Helpers/             # Pure logic helpers

FlipKit.Infrastructure/  # External integrations
├── Data/                   # EF Core, repositories
├── Services/               # Service implementations
├── ApiModels/              # API DTOs
└── Migrations/             # EF migrations
```

**Migration Steps:**
1. Create new projects
2. Move files to appropriate projects
3. Update namespaces
4. Update ViewLocator (Core.ViewModels → App.Views)
5. Fix all references
6. Test thoroughly

**Breaking Changes:**
- Namespace changes require ViewLocator update
- DI registration moves to App project

### 3. Unit and Integration Tests

**Priority:** High
**Effort:** Medium (3-4 weeks)
**Benefits:** Confidence in refactoring, catch regressions

**Test Projects:**
```
FlipKit.Core.Tests/
├── ViewModels/          # ViewModel unit tests
│   ├── ScanViewModelTests.cs
│   ├── InventoryViewModelTests.cs
│   └── PricingViewModelTests.cs
├── Helpers/
│   └── FuzzyMatcherTests.cs
└── Services/            # Mock-based tests
    └── CardRepositoryTests.cs

FlipKit.Infrastructure.Tests/
├── Data/
│   └── CardRepositoryIntegrationTests.cs
└── Services/
    ├── OpenRouterScannerTests.cs  # Recorded responses
    └── CsvExportServiceTests.cs
```

**Testing Strategy:**
- Unit tests: ViewModels with mocked services
- Integration tests: Database operations with in-memory SQLite
- API tests: Use recorded responses (VCR pattern)
- UI tests: Avalonia.Headless for critical flows

**Coverage Goals:**
- ViewModels: 80%+
- Services: 70%+
- Helpers: 90%+

### 4. Automated Price Scraping

**Priority:** High
**Effort:** High (4-6 weeks)
**Benefits:** Eliminates manual Terapeak/eBay lookups

**Current:** Opens browser, user manually checks prices
**Target:** Automated price lookup with multiple sources

**Approach Options:**

**Option A: eBay API (Recommended)**
- Use eBay Finding API for sold listings
- Requires eBay Developer account (free)
- Rate limits: 5,000 calls/day
- Pros: Official, reliable, no scraping issues
- Cons: Requires approval, API key management

**Option B: Web Scraping**
- Scrape eBay sold listings HTML
- Use HtmlAgilityPack or AngleSharp
- Pros: No API key needed
- Cons: Fragile (breaks when eBay changes HTML), legal gray area

**Option C: Terapeak Research API**
- Official eBay seller tool
- Requires eBay Store subscription ($30/month)
- Best data quality
- Pros: Most accurate pricing
- Cons: Subscription cost, limited to eBay sellers

**Recommended Implementation:**
```csharp
public interface IPriceScraperService
{
    Task<PriceDataResult> GetMarketPriceAsync(Card card);
}

public class PriceDataResult
{
    public decimal MedianSoldPrice { get; set; }
    public decimal AverageSoldPrice { get; set; }
    public int SoldCount { get; set; }
    public DateTime DataAsOf { get; set; }
    public List<RecentSale> RecentSales { get; set; }
}
```

**UI Changes:**
- PricingView: Add "Get Market Price" button
- Automatically populate EstimatedValue
- Show confidence interval (e.g., "$12-18 based on 15 sales")
- Cache results for 24 hours

**Configuration:**
- Settings → eBay API key
- Toggle auto-price vs manual
- Set price source priority (eBay, COMC, 130point)

---

## Medium Priority (6-12 Months)

### 5. Cloud Sync and Backup

**Priority:** Medium
**Effort:** High (6-8 weeks)
**Benefits:** Multi-device access, automatic backup

**Approach Options:**

**Option A: Dropbox/OneDrive Sync**
- Store cards.db in cloud folder
- Let OS handle sync
- Pros: Simple, no backend needed
- Cons: Conflicts with simultaneous edits, requires user setup

**Option B: Custom Cloud Backend**
- Build REST API (ASP.NET Core)
- Store data in cloud SQL database
- Implement conflict resolution
- Pros: Full control, real-time sync
- Cons: High effort, hosting costs

**Option C: Supabase/Firebase**
- Use BaaS for data sync
- Built-in authentication
- Pros: Fast to implement, real-time
- Cons: Vendor lock-in, monthly costs

**Recommended: Hybrid Approach**
1. Keep local SQLite as primary (offline-first)
2. Add optional cloud sync via Supabase
3. Manual sync button + automatic on app close
4. Conflict resolution: last-write-wins with user prompt

### 6. Additional Export Formats

**Priority:** Medium
**Effort:** Medium (2-3 weeks per format)

**Target Platforms:**

**eBay Bulk Upload CSV:**
- Different column format than Whatnot
- Requires eBay category mapping
- Support for item specifics
- Shipping policies

**COMC (Check Out My Cards):**
- CSV format for consignment
- Requires specific categorization
- Graded card support

**MySlabs (Graded Cards):**
- Focus on PSA/BGS graded
- Certification number validation
- Population report integration

**TCGPlayer (if expanding to TCG):**
- Trading card games (Pokemon, Magic, etc.)
- Different data model (set/card number)

### 7. Performance Optimizations

**Priority:** Medium
**Effort:** Medium (3-4 weeks)

**Current Issues:**
- InventoryView slows down with 500+ cards
- Full card list loaded into memory
- No pagination or virtualization

**Optimizations:**

**Database Indexes:**
```sql
CREATE INDEX idx_card_status ON Cards(Status);
CREATE INDEX idx_card_sport ON Cards(Sport);
CREATE INDEX idx_card_player ON Cards(PlayerName);
CREATE INDEX idx_card_pricedate ON Cards(PriceDate);
```

**Pagination:**
- Load 50 cards at a time
- Virtual scrolling in DataGrid
- Background loading with progress indicator

**Lazy Loading:**
- Don't load images until visible
- Thumbnail generation and caching
- Use ImageBrush with UriSource

**Caching:**
- Cache frequently accessed data (checklists)
- In-memory cache with expiration
- Reduce database round-trips

### 8. Dark Theme Support

**Priority:** Medium
**Effort:** Low (1-2 weeks)

**Implementation:**
- Avalonia supports theme switching
- Create dark version of AppStyles.axaml
- Add theme toggle in Settings
- Persist preference
- System theme detection (Windows/macOS)

**Color Palette:**
- Light theme: Current Fluent theme
- Dark theme: Dark background, light text
- Accent colors: Keep brand colors
- Ensure sufficient contrast (WCAG AA)

---

## Low Priority (Future Considerations)

### 9. Mobile Companion App

**Priority:** Low
**Effort:** Very High (3-6 months)

**Purpose:** Quick card scanning on the go with phone camera

**Tech Stack Options:**
- React Native (cross-platform)
- .NET MAUI (share code with desktop)
- Flutter (good camera support)

**Features:**
- Take photo → upload → AI scan
- View inventory (read-only)
- Quick price check
- Sync with desktop app

**Challenges:**
- Different UI paradigm (mobile vs desktop)
- Camera integration
- Network sync complexity
- Maintenance burden (2 codebases)

**Alternative:** Progressive Web App (PWA)
- Web-based, works on any device
- Camera API support
- Offline capability
- No app store approval needed

### 10. Barcode/QR Code Scanning

**Priority:** Low
**Effort:** Medium (2-3 weeks)

**Use Case:** Scan graded card slabs with QR/barcode

**Implementation:**
- Use ZXing.Net for barcode reading
- PSA, BGS have cert lookup APIs
- Auto-fill grading data from certification

**Challenge:** Most raw cards don't have barcodes

### 11. Price Alerts and Notifications

**Priority:** Low
**Effort:** Medium (2-3 weeks)

**Features:**
- Alert when card value changes significantly
- Notify when stale prices need updating
- Desktop notifications or email
- Configurable thresholds

**Requires:**
- Background service or scheduled task
- Price tracking over time
- Email/notification infrastructure

### 12. Multi-User / Team Features

**Priority:** Low
**Effort:** Very High (4-6 months)

**Use Case:** Card shops with multiple employees

**Features:**
- User accounts and permissions
- Activity log (who scanned/sold what)
- Team inventory management
- Role-based access (admin, employee, viewer)

**Challenges:**
- Shifts from single-user to multi-tenant
- Authentication and authorization
- Database schema changes
- Hosting requirements

---

## Technical Debt and Maintenance

### Code Quality Improvements

**Current Issues:**
- Some ViewModels are large (500+ lines)
- Limited error handling in some services
- Magic strings for API endpoints
- Hardcoded timeouts

**Improvements:**
- Refactor large ViewModels into smaller pieces
- Centralized error handling middleware
- Configuration-driven API endpoints
- Configurable timeouts and retries

### Documentation

**Needed:**
- API documentation (if adding backend)
- Inline XML comments for public APIs
- Architecture decision records (ADRs)
- End-user manual (screenshots + walkthroughs)

### Dependencies

**Regular Updates:**
- Avalonia (currently 11.3.11)
- CommunityToolkit.Mvvm
- EF Core
- NuGet packages (check for security updates)

**Breaking Changes:**
- Avalonia 12+ when released
- .NET 9/10 migration
- Plan upgrade path

---

## Community and Open Source

### Potential Contributions

**Areas for Community Help:**
- Additional checklist data (more sets, years)
- Bug reports and testing
- Feature requests and prioritization
- Translations (internationalization)

### Licensing Considerations

**Current:** Proprietary (private repo)
**Future:** Consider open-sourcing under MIT or Apache 2.0

**Benefits:**
- Community contributions
- Faster bug fixes
- Trust and transparency
- Portfolio/resume value

**Concerns:**
- Support burden
- Quality control
- Competitor cloning

---

## Success Metrics

### Usage Metrics (If Implemented)

- Active users (daily/monthly)
- Cards scanned per user
- Export success rate
- Feature adoption rates
- Crash reports and errors

### Quality Metrics

- Unit test coverage (target: 70%+)
- Bug report count (trend down)
- Average time to fix bugs
- User satisfaction (if surveys)

### Performance Metrics

- App startup time (< 2 seconds)
- Scan time (< 10 seconds per card)
- Export time (< 5 seconds for 50 cards)
- Database query time (< 100ms)

---

## Decision Framework

### Prioritization Criteria

When deciding what to build next:

1. **User Impact:** Does it solve a major pain point?
2. **Effort:** How long will it take? ROI?
3. **Risk:** Could it break existing features?
4. **Dependencies:** Does it block other features?
5. **Maintenance:** Ongoing cost to support?

### Feature Evaluation Template

For each proposed feature:

```
Feature: [Name]
Problem: [What pain does it solve?]
Users Affected: [How many? Who?]
Current Workaround: [How do users solve it today?]
Effort: [Hours/weeks estimate]
Risk: [Low/Medium/High]
Recommendation: [Build now / Later / Never]
```

---

## Conclusion

FlipKit has achieved MVP status with a solid foundation. The roadmap focuses on:

1. **Short-term:** Complete bulk scanning, refactor architecture, add tests
2. **Medium-term:** Automated pricing, cloud sync, more export formats
3. **Long-term:** Mobile apps, advanced features, scale

This is a living document. Priorities will shift based on user feedback, technical discoveries, and market changes. Revisit quarterly.

---

**Last Updated:** May 2026
**Next Review:** August 2026

**Recent additions:**
- 2026-05-01 — Added "User-Driven Checklist Excel Import" (item 0) — see [28-CHECKLIST-INSIDER-IMPORT-PLAN.md](28-CHECKLIST-INSIDER-IMPORT-PLAN.md)

# Phase 3 Testing Plan: Polish & Production Readiness

**Branch:** `feature/web-app-migration`
**Status:** In Progress
**Goal:** Verify all features work correctly, optimize performance, improve error handling, and prepare for production deployment.

## Testing Environment

- **Local Server:** http://localhost:5000 (http://0.0.0.0:5000)
- **Database:** `%APPDATA%\FlipKit\cards.db` (SQLite with WAL mode)
- **Target Platforms:**
  - Desktop browsers: Chrome, Edge, Firefox
  - Mobile browsers: Chrome Android, Safari iOS
  - Desktop app: Avalonia (concurrent testing)

## Phase 3 Objectives

1. **Functional Testing** - Verify all features work end-to-end
2. **Concurrent Access Testing** - Test Desktop + Web simultaneous usage
3. **Mobile Optimization** - Verify responsive design and camera integration
4. **Performance Testing** - Measure page load times and database query performance
5. **Error Handling** - Improve validation messages and edge case handling
6. **Security Review** - Basic security checks (XSS, SQL injection prevention)
7. **Documentation** - User guide and deployment instructions

---

## 1. Functional Testing Checklist

### Home Dashboard
- [ ] Dashboard displays correct card counts (total, by status)
- [ ] Financial summary shows accurate totals (inventory value, revenue, profit)
- [ ] Quick action buttons navigate to correct pages
- [ ] Page loads in < 3 seconds with 100 cards
- [ ] Empty state displays correctly with 0 cards

### Inventory Management
- [ ] Index page lists all cards in table format
- [ ] Search filters by player name (partial match)
- [ ] Sport filter shows correct cards
- [ ] Status filter works for all statuses (Draft, Priced, Ready, Listed, Sold)
- [ ] Pagination works (20 cards per page)
- [ ] Badges display correctly (Rookie, Auto, Graded)
- [ ] Details page shows all card information
- [ ] Images display correctly (ImageUrl1, ImageUrl2)
- [ ] Edit page loads with prepopulated data
- [ ] Edit form validation works (required fields, data types)
- [ ] Edit saves changes to database
- [ ] Delete confirmation modal appears
- [ ] Delete removes card from database
- [ ] TempData success/error messages display correctly

### Scan Feature
- [ ] Upload form displays with model dropdown (8 models)
- [ ] Front image upload works (file selection)
- [ ] Back image upload works (optional)
- [ ] Mobile camera capture works on phone (`capture="environment"`)
- [ ] Image preview displays before submission
- [ ] Loading spinner shows during AI scan (30-60s)
- [ ] AI scan returns valid JSON with card data
- [ ] Verification runs and displays confidence level
- [ ] High confidence shows green alert
- [ ] Medium confidence shows yellow alert with suggestions
- [ ] Low confidence shows red alert with warning
- [ ] Results page displays scanned card data
- [ ] Images upload to ImgBB successfully
- [ ] Save button adds card to inventory
- [ ] Discard button deletes temp images
- [ ] Error handling for invalid images
- [ ] Error handling for AI API failures

### Pricing Research
- [ ] Index lists cards with Status = Draft or Priced
- [ ] "Research Price" button navigates to Research page
- [ ] Card details display correctly
- [ ] External research links open in new tab:
  - [ ] Terapeak URL constructed correctly
  - [ ] eBay Sold URL constructed correctly
- [ ] Market value input accepts decimal values
- [ ] Listing price input accepts decimal values
- [ ] Profit calculator updates in real-time (JavaScript)
- [ ] Profit calculator shows correct breakdown:
  - [ ] Listing price
  - [ ] Fees (11%)
  - [ ] Net revenue
  - [ ] Cost basis
  - [ ] Net profit
  - [ ] Margin percentage
- [ ] Suggested price calculation AJAX endpoint works
- [ ] Save updates card with EstimatedValue and ListingPrice
- [ ] Save changes Status to Priced
- [ ] TempData success message displays after save

### Export to CSV
- [ ] Index shows two sections: Ready cards and Priced cards
- [ ] Ready cards section displays correct count
- [ ] Priced cards section displays correct count
- [ ] "Mark as Ready" button changes Status to Ready
- [ ] "Mark as Ready" moves card from Priced to Ready section
- [ ] "Generate Whatnot CSV" button appears when ReadyCards.Any()
- [ ] CSV generation validates all cards
- [ ] Validation errors display in TempData
- [ ] CSV file downloads successfully
- [ ] CSV format matches Whatnot specification
- [ ] CSV contains all required fields
- [ ] Preview button navigates to Preview page
- [ ] Preview displays generated title
- [ ] Preview displays generated description
- [ ] Preview shows validation errors if any
- [ ] Preview shows card images
- [ ] Validation endpoint works via AJAX

### Reports & Analytics
- [ ] Main dashboard displays inventory statistics:
  - [ ] Total cards
  - [ ] Cards by status (Draft, Priced, Ready, Listed, Sold)
  - [ ] Cards by sport (breakdown with percentages)
- [ ] Financial overview displays:
  - [ ] Active inventory value
  - [ ] Active inventory cost
  - [ ] Total revenue (sold cards)
  - [ ] Total profit
- [ ] Recent sales table shows last 30 days
- [ ] Recent sales displays correctly with profit calculation
- [ ] Sales report date range filter works
- [ ] Sales report displays correct totals:
  - [ ] Total sales count
  - [ ] Total revenue
  - [ ] Total profit
  - [ ] Average profit
- [ ] Sales detail table shows all sold cards in range
- [ ] Financial report displays overall stats:
  - [ ] Total inventory value/cost
  - [ ] Total revenue/profit
  - [ ] Overall profit margin
  - [ ] Inventory turnover rate
- [ ] Profitability by sport table displays correctly
- [ ] Profit distribution visual (progress bars) works
- [ ] All charts and tables are responsive

---

## 2. Concurrent Access Testing

### Desktop + Web Simultaneous Usage

**Goal:** Verify shared database works without locking errors.

**Scenario 1: Concurrent Reads**
- [ ] Open Desktop app and Web app simultaneously
- [ ] Load Inventory in Desktop app
- [ ] Load Inventory in Web app
- [ ] Verify both show same cards
- [ ] No database lock errors

**Scenario 2: Desktop Write, Web Read**
- [ ] Add new card in Desktop app (Scan page)
- [ ] Refresh Inventory in Web app
- [ ] Verify new card appears in Web
- [ ] No delay or errors

**Scenario 3: Web Write, Desktop Read**
- [ ] Add new card in Web app (Scan upload)
- [ ] Refresh Inventory in Desktop app
- [ ] Verify new card appears in Desktop
- [ ] No delay or errors

**Scenario 4: Simultaneous Writes (Conflict Test)**
- [ ] Edit same card in Desktop app
- [ ] Edit same card in Web app at same time
- [ ] Save in Desktop first
- [ ] Save in Web second
- [ ] Verify one update succeeds (last write wins or retry)
- [ ] No data corruption

**Scenario 5: Settings Sync**
- [ ] Change settings in Desktop app (e.g., default model)
- [ ] Restart Web app
- [ ] Verify settings are reflected in Web
- [ ] Change settings in Web
- [ ] Verify Desktop picks up changes (with reload)

**Expected Results:**
- WAL mode should prevent most locking errors
- Concurrent reads should always work
- Concurrent writes may require retry logic (future enhancement)
- No data corruption under any scenario

---

## 3. Mobile Optimization Testing

### Responsive Design
- [ ] Test on phone (375px width minimum)
- [ ] Test on tablet (768px width)
- [ ] Navigation bar collapses to hamburger menu on mobile
- [ ] Tables are horizontally scrollable on small screens
- [ ] Forms are touch-friendly (larger inputs, buttons)
- [ ] Cards and modals fit within viewport
- [ ] No horizontal scrolling on any page

### Camera Integration (Mobile Only)
- [ ] Scan upload page opens camera on Android Chrome
- [ ] Scan upload page opens camera on iOS Safari
- [ ] Camera captures front image correctly
- [ ] Camera captures back image correctly
- [ ] Image preview displays after capture
- [ ] File upload fallback works if camera unavailable

### Performance on Mobile
- [ ] Dashboard loads in < 5 seconds on 4G
- [ ] Inventory page loads in < 5 seconds on 4G
- [ ] Scan results display in < 5 seconds after upload
- [ ] No jank or lag during scrolling
- [ ] Touch targets are at least 44x44px

---

## 4. Performance Testing

### Page Load Times (Target: < 3s on desktop, < 5s on mobile)

| Page | Empty DB | 100 Cards | 500 Cards | 1000 Cards |
|------|----------|-----------|-----------|------------|
| Home Dashboard | | | | |
| Inventory Index | | | | |
| Scan Upload | | | | |
| Pricing Research | | | | |
| Export Index | | | | |
| Reports Dashboard | | | | |

### Database Query Performance
- [ ] Index page query time: < 500ms with 1000 cards
- [ ] Search query time: < 500ms
- [ ] Filter query time: < 500ms
- [ ] Dashboard stats aggregation: < 1s
- [ ] Reports profitability calculation: < 2s

### Optimization Opportunities
- [ ] Add database indexes for commonly filtered columns (Sport, Status, Year)
- [ ] Implement response caching for static data
- [ ] Add pagination server-side (currently client-side)
- [ ] Lazy load images in Inventory list
- [ ] Minify CSS/JS in production build

---

## 5. Error Handling Testing

### Validation Errors
- [ ] Required field validation works (client-side HTML5)
- [ ] Data type validation works (numeric fields, dates)
- [ ] Custom validation messages display clearly
- [ ] Error messages are user-friendly (not technical)

### API Failures
- [ ] OpenRouter API failure shows helpful message
- [ ] ImgBB upload failure shows error
- [ ] eBay API timeout handled gracefully
- [ ] Network errors display "Try again" message

### Database Errors
- [ ] Database lock error displays helpful message
- [ ] Constraint violation (e.g., duplicate) shows clear error
- [ ] Transaction rollback works correctly

### Edge Cases
- [ ] Upload image > 10MB shows size limit error
- [ ] Upload non-image file shows format error
- [ ] Submit form with null/empty values validates correctly
- [ ] Delete non-existent card returns 404
- [ ] Edit non-existent card returns 404

### Global Error Handling
- [ ] Unhandled exceptions show generic error page
- [ ] Error page includes link to return to dashboard
- [ ] Errors are logged to console/file (Serilog)

---

## 6. Security Review

### Input Validation
- [ ] All user inputs are validated server-side
- [ ] Numeric inputs reject non-numeric values
- [ ] Date inputs reject invalid dates
- [ ] File uploads reject non-image types (scan)

### SQL Injection Prevention
- [ ] All database queries use parameterized queries (EF Core handles this)
- [ ] No raw SQL with string concatenation
- [ ] Test with malicious inputs (e.g., `'; DROP TABLE cards; --`)

### XSS Prevention
- [ ] User inputs are HTML-encoded in Razor views (`@Model.PlayerName`)
- [ ] JavaScript strings are properly escaped
- [ ] No `@Html.Raw()` on user-generated content
- [ ] Test with script injection: `<script>alert('XSS')</script>`

### CSRF Protection
- [ ] All POST forms include `@Html.AntiForgeryToken()`
- [ ] `[ValidateAntiForgeryToken]` attribute on all POST actions
- [ ] Test form submission without token (should fail)

### File Upload Security
- [ ] Only image files accepted (MIME type check)
- [ ] File size limit enforced (10MB max)
- [ ] Uploaded files saved with generated GUIDs (not user-provided names)
- [ ] Images stored outside webroot or with restricted access

### Authentication (Future)
- [ ] Currently no authentication (localhost only)
- [ ] Add login system before deploying to network
- [ ] Use ASP.NET Core Identity or similar

---

## 7. Browser Compatibility Testing

### Desktop Browsers
- [ ] Chrome (latest)
- [ ] Edge (latest)
- [ ] Firefox (latest)
- [ ] Safari (macOS, if available)

### Mobile Browsers
- [ ] Chrome Android (latest)
- [ ] Safari iOS (latest)
- [ ] Firefox Mobile
- [ ] Samsung Internet

### Known Compatibility Issues
- (Document any browser-specific issues here)

---

## 8. Accessibility Testing (Basic)

- [ ] All images have alt text
- [ ] Forms have proper labels
- [ ] Color contrast meets WCAG AA standards
- [ ] Keyboard navigation works (tab through forms)
- [ ] Screen reader test (basic navigation)

---

## 9. Documentation Tasks

### User Documentation
- [ ] Create USER-GUIDE.md with screenshots
- [ ] Document scan workflow (upload → results → save)
- [ ] Document pricing workflow (research → set price)
- [ ] Document export workflow (mark ready → generate CSV)
- [ ] Document reports usage

### Deployment Documentation
- [ ] How to run on localhost
- [ ] How to access from phone on local network (http://192.168.x.x:5000)
- [ ] How to configure firewall for local network access
- [ ] How to set up HTTPS (optional)
- [ ] How to deploy to production (IIS, Docker, etc.)

### Developer Documentation
- [ ] Update CLAUDE.md with web project structure
- [ ] Document DI service lifetimes and why
- [ ] Document platform-specific service patterns
- [ ] Document database sharing architecture

---

## 10. Known Issues & Limitations

### Current Limitations
1. **No Authentication** - Anyone on local network can access
2. **No Real-Time Sync** - Changes require manual refresh
3. **No PWA** - Cannot install as app on phone home screen
4. **No Bulk Scan** - Web doesn't support multi-card batch scanning
5. **No Image Editing** - Cannot crop/rotate images in web UI
6. **Limited Error Recovery** - Some errors require page refresh

### Technical Debt
1. **Nullability Warnings** - 9 compiler warnings to suppress/fix
2. **Hardcoded Values** - Fee percentage (11%), page size (20) should be configurable
3. **No Caching** - Repeated database queries could be cached
4. **No Rate Limiting** - API calls not rate-limited (could exceed free tier)
5. **Manual Testing Only** - No automated tests yet

### Browser Issues
- (To be filled in during testing)

---

## Testing Progress Tracker

**Phase 3.1: Functional Testing** (Estimated: 4-6 hours)
- [x] Fix DI scoping issues
- [x] Fix database initialization
- [x] Verify app starts successfully
- [ ] Test Home Dashboard
- [ ] Test Inventory CRUD
- [ ] Test Scan workflow
- [ ] Test Pricing workflow
- [ ] Test Export workflow
- [ ] Test Reports

**Phase 3.2: Concurrent Access** (Estimated: 2-3 hours)
- [ ] Test Desktop + Web simultaneous reads
- [ ] Test Desktop write → Web read
- [ ] Test Web write → Desktop read
- [ ] Test simultaneous writes (conflict handling)
- [ ] Test settings sync

**Phase 3.3: Mobile Testing** (Estimated: 2-3 hours)
- [ ] Test responsive design on phone
- [ ] Test responsive design on tablet
- [ ] Test camera integration (Android)
- [ ] Test camera integration (iOS)
- [ ] Measure mobile performance

**Phase 3.4: Performance & Optimization** (Estimated: 3-4 hours)
- [ ] Measure page load times
- [ ] Identify slow queries
- [ ] Add database indexes
- [ ] Optimize views
- [ ] Test with large dataset (1000+ cards)

**Phase 3.5: Error Handling** (Estimated: 2-3 hours)
- [ ] Test all validation scenarios
- [ ] Test API failure handling
- [ ] Improve error messages
- [ ] Add global error handler

**Phase 3.6: Security Review** (Estimated: 1-2 hours)
- [ ] Test XSS prevention
- [ ] Test CSRF protection
- [ ] Test file upload security
- [ ] Review input validation

**Phase 3.7: Documentation** (Estimated: 2-3 hours)
- [ ] Create user guide
- [ ] Create deployment guide
- [ ] Update developer docs
- [ ] Create troubleshooting guide

**Total Estimated Time: 16-24 hours**

---

## Success Criteria

Phase 3 is complete when:

- [ ] All functional tests pass (100%)
- [ ] Desktop + Web concurrent usage works without errors
- [ ] Mobile browser compatibility verified (Android Chrome, iOS Safari)
- [ ] Page load times meet targets (< 3s desktop, < 5s mobile)
- [ ] Critical security checks pass (XSS, CSRF, SQL injection)
- [ ] User documentation complete
- [ ] Deployment guide written
- [ ] Known issues documented
- [ ] Ready for user acceptance testing

---

## Next Steps After Phase 3

1. **Merge to Master** - If all tests pass, merge feature branch
2. **User Acceptance Testing** - Deploy to local network, test with real usage
3. **Bug Fixes** - Address any issues found during UAT
4. **Production Deployment** - Deploy to final environment
5. **Future Enhancements** - Authentication, PWA, real-time sync, bulk scan

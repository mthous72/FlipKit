# Avalonia MVVM Desktop App Architecture

## Overview

The app is a **native cross-platform desktop application** built with Avalonia UI and the MVVM pattern. Users double-click an executable — no Python, no browser, no runtime dependencies.

**Key Principles:**
- **Local-first:** All data stays on user's computer
- **User owns credentials:** They create their own API accounts
- **Simple setup:** First-run wizard for non-technical users
- **KISS:** Keep It Simple, Stupid — one obvious way to do each task
- **No Norman Doors:** UI does what it looks like it does
- **MVVM:** ViewModels are testable, Views are declarative XAML

---

## Why Avalonia + MVVM

| Factor | Avalonia MVVM | NiceGUI (Python/Browser) | WPF |
|--------|--------------|--------------------------|-----|
| Cross-platform | ✅ Win/Mac/Linux | ✅ Anywhere with browser | ❌ Windows only |
| Native feel | ✅ True desktop window | ❌ Browser tab | ✅ Native |
| No runtime deps | ✅ Self-contained exe | ❌ Needs Python installed | ✅ But Windows only |
| Modern UI | ✅ Fluent theme, XAML | ✅ Quasar/Vue | ✅ But dated patterns |
| Testable logic | ✅ ViewModels unit-testable | ❌ No formal separation | ✅ Same MVVM |
| Distribution | ✅ Single file publish | ❌ Folder + install steps | ✅ But Windows only |
| XAML ecosystem | ✅ Shared skills with WPF/MAUI | ❌ Different paradigm | ✅ Origin of XAML |

**How it works:**
1. User double-clicks `FlipKit.exe` (Windows) or `FlipKit.app` (Mac)
2. Native window appears immediately
3. First run → Setup Wizard guides API key configuration
4. Everything runs on their machine, data stored locally in SQLite

---

## Dependency Injection Setup

All services and ViewModels are registered in `App.axaml.cs`:

```csharp
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        // Database
        services.AddDbContext<FlipKitDbContext>(options =>
            options.UseSqlite($"Data Source={GetDbPath()}"));

        // Services
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddTransient<ICardRepository, CardRepository>();
        services.AddHttpClient<IScannerService, OpenRouterScannerService>();
        services.AddHttpClient<IImageUploadService, ImgBBUploadService>();
        services.AddTransient<IPricerService, PricerService>();
        services.AddTransient<IExportService, CsvExportService>();
        services.AddSingleton<IBrowserService, SystemBrowserService>();
        services.AddSingleton<IFileDialogService, AvaloniaFileDialogService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ScanViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<PricingViewModel>();
        services.AddTransient<ExportViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SetupWizardViewModel>();
        services.AddTransient<RepriceViewModel>();

        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

---

## ViewLocator Pattern

Maps ViewModels to Views automatically by naming convention:

```csharp
public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null) return new TextBlock { Text = "No data" };

        var name = data.GetType().FullName!
            .Replace("Core.ViewModels", "App.Views")
            .Replace("ViewModel", "View");

        var type = Type.GetType(name);
        if (type != null) return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = $"View not found: {name}" };
    }

    public bool Match(object? data) => data is ObservableObject;
}
```

Registered in `App.axaml`:
```xml
<Application.DataTemplates>
    <local:ViewLocator />
</Application.DataTemplates>
```

---

## Navigation Architecture

`MainWindowViewModel` manages page navigation:

```csharp
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly ISettingsService _settings;

    [ObservableProperty] private ObservableObject _currentPage;
    [ObservableProperty] private bool _isFirstRun;

    public MainWindowViewModel(IServiceProvider services, ISettingsService settings)
    {
        _services = services;
        _settings = settings;

        IsFirstRun = !_settings.HasValidConfig();
        CurrentPage = IsFirstRun
            ? _services.GetRequiredService<SetupWizardViewModel>()
            : _services.GetRequiredService<ScanViewModel>();
    }

    [RelayCommand]
    private void NavigateTo(string page)
    {
        CurrentPage = page switch
        {
            "Scan"      => _services.GetRequiredService<ScanViewModel>(),
            "Inventory" => _services.GetRequiredService<InventoryViewModel>(),
            "Pricing"   => _services.GetRequiredService<PricingViewModel>(),
            "Export"    => _services.GetRequiredService<ExportViewModel>(),
            "Settings"  => _services.GetRequiredService<SettingsViewModel>(),
            "Reprice"   => _services.GetRequiredService<RepriceViewModel>(),
            _ => CurrentPage
        };
    }
}
```

`MainWindow.axaml` hosts sidebar + content area:
```xml
<Window>
    <DockPanel>
        <!-- Sidebar Navigation -->
        <StackPanel DockPanel.Dock="Left" Width="80" Background="{DynamicResource SystemChromeLowColor}">
            <Button Command="{Binding NavigateToCommand}" CommandParameter="Scan">
                <StackPanel><PathIcon Data="{StaticResource CameraIcon}"/><TextBlock Text="Scan"/></StackPanel>
            </Button>
            <Button Command="{Binding NavigateToCommand}" CommandParameter="Inventory">
                <StackPanel><PathIcon Data="{StaticResource ListIcon}"/><TextBlock Text="Cards"/></StackPanel>
            </Button>
            <!-- ... more nav buttons ... -->
        </StackPanel>

        <!-- Active Page Content -->
        <ContentControl Content="{Binding CurrentPage}" />
    </DockPanel>
</Window>
```

The `ViewLocator` automatically resolves the correct View for whatever ViewModel is in `CurrentPage`.

---

## App File Structure

```
FlipKit.App/
├── App.axaml                          # Theme, resources, ViewLocator
├── App.axaml.cs                       # DI container, startup logic
├── Program.cs                         # Entry point
├── ViewLocator.cs                     # ViewModel → View resolver
│
├── Views/
│   ├── MainWindow.axaml               # Shell: sidebar + content area
│   ├── ScanView.axaml                 # Drop zone + preview + form + save
│   ├── InventoryView.axaml            # DataGrid + filters + bulk actions
│   ├── PricingView.axaml              # Card picker + research + price input
│   ├── ExportView.axaml               # Upload images + download CSV
│   ├── SettingsView.axaml             # API keys + preferences + data mgmt
│   ├── SetupWizardView.axaml          # First-run 3-step setup
│   ├── CardDetailView.axaml           # Reusable card edit form (UserControl)
│   └── RepriceView.axaml              # Stale card queue
│
├── Styles/
│   ├── AppStyles.axaml                # Brand colors, typography, spacing
│   └── Controls.axaml                 # Custom control templates
│
├── Converters/                         # Authoritative list lives in
│   │                                    # FlipKit.Desktop/Converters/; sample shown below
│   ├── PriceAgeToColorConverter.cs    # Days since priced → 🟢🟡🔴 brush
│   ├── StatusToBadgeConverter.cs      # CardStatus → display string/color
│   ├── CurrencyFormatConverter.cs     # decimal → "$12.99"
│   └── NullToVisibilityConverter.cs   # null check → show/hide
│   # NOTE: BoolToVisibilityConverter was deleted in Phase 3 — no XAML reference found.
│
└── Assets/
    ├── logo.png
    └── Icons/                         # SVG path data for sidebar icons
```

---

## Key Views Detail

### 1. Scan View

**Purpose:** Upload image → AI scan → review → save to inventory

**Layout (two-column):**
- Left: Image drop zone / preview (accepts drag & drop or file browse)
- Right: Card detail form (populates after scan)
- Bottom: "Scan Card" button → "Save to My Cards" button

**ViewModel bindings:**
- `ImagePath` → image preview
- `IsScanning` → spinner overlay + disabled buttons
- `ScannedCard` (CardDetailViewModel) → form fields
- `ErrorMessage` → error banner with "Try Again" / "Enter Manually" options
- `ScanCardCommand` → async relay command
- `SaveCardCommand` → async relay command

**Drag & drop:** Avalonia supports `DragDrop.Drop` attached events on any control.

### 2. Inventory View

**Purpose:** View, filter, search, edit, and bulk-manage all cards

**Layout:**
- Top bar: Search TextBox + Sport ComboBox + Status ComboBox + Price Age filter
- Center: DataGrid with columns: checkbox, thumbnail, player, year, brand, price, price age indicator, status
- Bottom: Bulk action buttons (Delete Selected, Mark Ready)
- Double-click row → opens card detail as a dialog or side panel

**Key features:**
- `CollectionViewSource` with filters for real-time filtering
- Price age column uses `PriceAgeToColorConverter` for 🟢🟡🔴 circles
- Badge on nav button shows total card count
- "Reprice Stale Cards (N)" button when stale cards exist

### 3. Pricing View

**Purpose:** Research comps and set prices, one card at a time

**Layout:**
- Card display: thumbnail + details
- Research buttons: "Open Terapeak" / "Open eBay Sold" (open system browser)
- Market value input → auto-calculated suggested price (accounting for fees)
- Listing price input (user can override)
- Cost basis section: cost, source dropdown, date, notes
- "Save & Next →" button for batch pricing flow

### 4. Export View

**Purpose:** Upload images to ImgBB and generate Whatnot CSV

**Layout (stepped):**
- Step 1: Filter cards (status = Ready), show count and preview table
- Step 2: "Upload Images" button with progress bar
- Step 3: "Download Whatnot CSV" button → native save dialog
- Step 4: Instructions for uploading to Whatnot Seller Hub + link button

### 5. Settings View

**Sections:**
- API Connections: OpenRouter key (masked) + test button, ImgBB key (masked) + test button
- Preferences: eBay seller toggle, default shipping profile, default condition
- Financial Settings: default platform fee %, default shipping costs, staleness threshold
- Your Data: card count, DB location, Open Folder, Backup, Clear All Data

### 6. Setup Wizard View

**Modal overlay on first run. Three steps:**

Step 1 — OpenRouter:
- Explanation of what it does
- "Create Free Account" link button (opens browser)
- API key paste field
- "Test Connection" button → ✅ Connected or ❌ Failed

Step 2 — ImgBB:
- Same pattern as step 1

Step 3 — Preferences:
- "I sell on eBay" checkbox (enables Terapeak links)
- Default shipping profile dropdown
- "Finish" button → dismisses wizard, navigates to Scan page

---

## Credential Storage

User API keys stored in a local JSON config file:

```
{AppDataFolder}/FlipKit/
├── config.json             ← API keys and preferences
├── cards.db                ← Card inventory (SQLite)
├── images/                 ← Local card photos
│   ├── front/
│   └── back/
├── exports/                ← Generated CSV files
└── logs/                   ← App logs
```

**config.json structure:**
```json
{
  "openRouterApiKey": "sk-or-v1-...",
  "imgbbApiKey": "...",
  "isEbaySeller": true,
  "defaultShippingProfile": "4 oz",
  "defaultCondition": "Near Mint",
  "whatnotFeePercent": 11.0,
  "ebayFeePercent": 13.25,
  "defaultShippingCostPwe": 1.00,
  "defaultShippingCostBmwt": 4.50,
  "priceStalenessThresholdDays": 30
}
```

**Security:**
- Keys stored in plain text (acceptable for local desktop app)
- File is in user's AppData/Application Support folder
- Never transmitted except to the respective API services
- User can delete config.json to reset and re-run wizard

---

## Service Interfaces

All business logic is accessed through interfaces (defined in `FlipKit.Core`):

```csharp
public interface ICardRepository
{
    Task<int> InsertCardAsync(Card card);
    Task UpdateCardAsync(Card card);
    Task<Card?> GetCardAsync(int id);
    Task<List<Card>> GetAllCardsAsync(CardStatus? status = null, Sport? sport = null);
    Task DeleteCardAsync(int id);
    Task<List<Card>> SearchCardsAsync(string query);
    Task<List<Card>> GetStaleCardsAsync(int thresholdDays);
    Task AddPriceHistoryAsync(PriceHistory history);
    Task<int> GetCardCountAsync();
}

public interface IScannerService
{
    Task<Card> ScanCardAsync(string imagePath, string model = "openai/gpt-4o-mini");
}

public interface IPricerService
{
    string BuildTerapeakUrl(Card card);
    string BuildEbaySoldUrl(Card card);
    decimal SuggestPrice(decimal estimatedValue, Card card);
    decimal CalculateNet(decimal salePrice, decimal feePercent = 11m);
}

public interface IImageUploadService
{
    Task<string> UploadImageAsync(string imagePath, string? name = null);
    Task<(string? url1, string? url2)> UploadCardImagesAsync(string frontPath, string? backPath = null);
}

public interface IExportService
{
    string GenerateTitle(Card card);
    string GenerateDescription(Card card);
    Task ExportCsvAsync(List<Card> cards, string outputPath);
    List<string> ValidateCardForExport(Card card);
    Task ExportTaxCsvAsync(List<Card> soldCards, string outputPath);
}

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
    bool HasValidConfig();
    Task<bool> TestOpenRouterConnectionAsync(string apiKey);
    Task<bool> TestImgBBConnectionAsync(string apiKey);
}

public interface IBrowserService
{
    void OpenUrl(string url);
}

public interface IFileDialogService
{
    Task<string?> OpenImageFileAsync();
    Task<string?> SaveCsvFileAsync(string defaultFileName);
}
```

---

## Development Notes for Claude Code

### Build Order (for iterative development)

1. **Create solution + 3 projects** — `dotnet new sln`, class libraries + Avalonia app
2. **Models + Enums** — Card, PriceHistory, AppSettings, CardStatus, Sport, CostSource
3. **Service interfaces** — All `I*` interfaces in Core
4. **EF Core DbContext + migrations** — Schema creation
5. **SettingsService** — config.json read/write (needed for everything else)
6. **MainWindow shell** — Sidebar nav + ContentControl + ViewLocator
7. **ScanView + ScanViewModel** — First working feature end-to-end
8. **InventoryView** — DataGrid with real data
9. **PricingView** — URL generation + price calculation
10. **ExportView** — CSV generation
11. **SetupWizard** — First-run experience
12. **Polish** — Styles, error handling, loading states

### Key Claude Code Tips

- Build one ViewModel + View pair at a time
- Test ViewModel logic independently (no UI needed)
- Use `[ObservableProperty]` and `[RelayCommand]` to minimize boilerplate
- Keep Views as pure XAML — if you're writing C# in code-behind, move it to the ViewModel
- Reference this plan's code samples in your prompts
- Test with real card images early

---

## Requirements Summary

**.NET SDK:** 8.0+
**IDE:** Any (VS Code, Rider, Visual Studio)
**Target platforms:** Windows x64, macOS arm64/x64, Linux x64
**Self-contained publish:** Yes (no .NET runtime required on user's machine)

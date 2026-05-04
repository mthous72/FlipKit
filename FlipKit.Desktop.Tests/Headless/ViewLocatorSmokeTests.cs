using System.Reflection;
using FlipKit.Desktop;
using FlipKit.Desktop.ViewModels;

namespace FlipKit.Desktop.Tests.Headless;

/// <summary>
/// "Headless" smoke tests — verify the MVVM resolution contract that drives runtime
/// view rendering, without needing the full Avalonia.Headless runtime setup. The
/// actual fragile point is the ViewLocator's name-based reflection: rename a VM
/// without renaming the View, and you get a "Not Found: ..." TextBlock at runtime
/// with no compiler warning.
///
/// Phase 4d originally planned full Avalonia.Headless smoke tests for app boot +
/// navigation. Per the cost/value tradeoff at write time: full headless setup
/// (AvaloniaTestApplication attribute, AppBuilder fixture, UI thread marshalling)
/// added significant complexity for minimal additional signal beyond what the
/// ViewLocator contract test provides. Phase 5.4 ViewModel decomposition will
/// likely re-evaluate whether richer UI smoke tests are worth the investment.
/// </summary>
public class ViewLocatorSmokeTests
{
    /// <summary>
    /// For every ViewModel that derives from ViewModelBase (i.e., is a navigable
    /// "page" VM), there must be a corresponding View type with the same FullName
    /// except "ViewModel" → "View". This is what ViewLocator.Build() resolves at
    /// runtime via reflection.
    /// </summary>
    [Fact]
    public void Should_HaveMatchingViewType_For_EveryNavigableViewModel()
    {
        var desktopAssembly = typeof(MainWindowViewModel).Assembly;
        var viewModelBaseType = typeof(ViewModelBase);

        // MainWindowViewModel is the top-level Window's DataContext (MainWindow.axaml),
        // never resolved through ViewLocator — it's the host, not a navigable page.
        var navigableVms = desktopAssembly.GetTypes()
            .Where(t => !t.IsAbstract && viewModelBaseType.IsAssignableFrom(t))
            .Where(t => t.FullName!.EndsWith("ViewModel"))
            .Where(t => t != typeof(MainWindowViewModel))
            .ToList();

        Assert.NotEmpty(navigableVms); // sanity: must find SOME VMs

        var missing = new List<string>();
        foreach (var vm in navigableVms)
        {
            var viewName = vm.FullName!.Replace("ViewModel", "View");
            var viewType = desktopAssembly.GetType(viewName);
            if (viewType == null)
                missing.Add($"{vm.FullName} → expected {viewName}");
        }

        Assert.True(missing.Count == 0,
            "ViewLocator name-based resolution would fail at runtime for these VMs:\n  " +
            string.Join("\n  ", missing));
    }

    [Fact]
    public void Should_MatchOnlyViewModelBaseDerivatives_When_AskedToMatchTypes()
    {
        var locator = new ViewLocator();

        Assert.True(locator.Match(new MainWindowViewModelStub()));
        Assert.False(locator.Match("a string"));
        Assert.False(locator.Match(42));
        Assert.False(locator.Match(null));
    }

    private sealed class MainWindowViewModelStub : ViewModelBase { }
}

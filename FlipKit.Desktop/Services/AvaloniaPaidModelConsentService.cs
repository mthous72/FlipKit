using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using FlipKit.Core.Services;
using FlipKit.Desktop.Views;

namespace FlipKit.Desktop.Services
{
    /// <summary>
    /// Avalonia implementation of <see cref="IPaidModelConsentService"/> — shows a
    /// modal picker over the main window so the user can choose which paid model to
    /// use (or cancel). Returns null on any failure (e.g. no main window) so the
    /// caller cancels the scan rather than crashing.
    /// </summary>
    public class AvaloniaPaidModelConsentService : IPaidModelConsentService
    {
        public async Task<OpenRouterModel?> AskAsync(
            IReadOnlyList<OpenRouterModel> availableModels,
            OpenRouterModel suggestedModel,
            string contextMessage)
        {
            try
            {
                return await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var owner = GetMainWindow();
                    if (owner == null) return null;

                    var dialog = new PaidModelConsentDialog(availableModels, suggestedModel, contextMessage);
                    await dialog.ShowDialog(owner);
                    return dialog.Chosen;
                });
            }
            catch
            {
                // Dialog couldn't be shown — fail closed (cancel scan).
                return null;
            }
        }

        private static Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;
            return null;
        }
    }
}

using System;
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
    /// modal dialog over the main window with the proposed paid model + estimated
    /// cost, and returns the user's Yes/No answer. Returns false on any failure
    /// (e.g. no main window) so the caller cancels the scan rather than crashing.
    /// </summary>
    public class AvaloniaPaidModelConsentService : IPaidModelConsentService
    {
        public async Task<bool> AskAsync(OpenRouterModel proposedModel, string contextMessage)
        {
            try
            {
                return await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var owner = GetMainWindow();
                    if (owner == null) return false;

                    var dialog = new PaidModelConsentDialog(proposedModel, contextMessage);
                    await dialog.ShowDialog(owner);
                    return dialog.Accepted;
                });
            }
            catch
            {
                // Dialog couldn't be shown — fail closed (cancel scan).
                return false;
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

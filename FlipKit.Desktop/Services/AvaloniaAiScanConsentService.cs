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
    /// Avalonia implementation of <see cref="IAiScanConsentService"/> — shows a
    /// modal dialog explaining that card images are sent to CardSight/OpenRouter,
    /// with an optional "remember this choice" checkbox. Returns false (cancel) on
    /// any failure so the caller aborts the scan rather than crashing.
    /// </summary>
    public class AvaloniaAiScanConsentService : IAiScanConsentService
    {
        public async Task<AiScanConsentResult> AskAsync()
        {
            try
            {
                return await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var owner = GetMainWindow();
                    if (owner == null) return new AiScanConsentResult(false, false);

                    var dialog = new AiScanConsentDialog();
                    await dialog.ShowDialog(owner);
                    return new AiScanConsentResult(dialog.Proceed, dialog.Remember);
                });
            }
            catch
            {
                return new AiScanConsentResult(false, false);
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

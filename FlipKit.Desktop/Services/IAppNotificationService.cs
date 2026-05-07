using Avalonia.Controls;

namespace FlipKit.Desktop.Services
{
    /// <summary>
    /// Shows in-app toast notifications. Must be initialized with the main window's
    /// TopLevel before use (called from App.axaml.cs after the window opens).
    /// </summary>
    public interface IAppNotificationService
    {
        void Initialize(TopLevel topLevel);
        void NotifyBulkScanComplete(int scanned, int errors);
    }
}

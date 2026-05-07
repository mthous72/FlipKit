using System;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using FlipKit.Core.Services;

namespace FlipKit.Desktop.Services
{
    /// <summary>
    /// Displays in-window toast notifications via Avalonia's WindowNotificationManager.
    /// Clicking a bulk-scan completion toast navigates back to the Bulk Scan tab.
    /// </summary>
    public class AvaloniaAppNotificationService : IAppNotificationService
    {
        private readonly INavigationService _navigation;
        private WindowNotificationManager? _manager;

        public AvaloniaAppNotificationService(INavigationService navigation)
        {
            _navigation = navigation;
        }

        public void Initialize(TopLevel topLevel)
        {
            _manager = new WindowNotificationManager(topLevel)
            {
                Position = NotificationPosition.BottomRight,
                MaxItems = 3,
            };
        }

        public void NotifyBulkScanComplete(int scanned, int errors)
        {
            if (_manager == null) return;

            Dispatcher.UIThread.Post(() =>
            {
                var type = errors > 0 ? NotificationType.Warning : NotificationType.Success;
                var title = "Bulk scan complete";
                var body = errors > 0
                    ? $"{scanned} card(s) scanned, {errors} failed. Click to view results."
                    : $"{scanned} card(s) scanned successfully. Click to view results.";

                _manager.Show(new Notification(
                    title, body, type,
                    expiration: TimeSpan.FromSeconds(12),
                    onClick: () => _ = _navigation.NavigateToBulkScanAsync()));
            });
        }
    }
}

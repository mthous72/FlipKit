using System;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FlipKit.Desktop.Services
{
    /// <summary>
    /// Displays in-window toast notifications via Avalonia's WindowNotificationManager.
    /// All toasts are clickable and navigate the user to the relevant tab so they
    /// don't have to hunt for context after seeing the alert.
    /// </summary>
    public class AvaloniaAppNotificationService : IAppNotificationService
    {
        // Default toast lifetime for non-sticky notifications. Matches the prior
        // behaviour (12s) chosen so users on another tab notice without forcing
        // dismissal during a long-running scan.
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromSeconds(12);

        // We resolve INavigationService lazily via IServiceProvider rather than
        // taking it as a ctor dependency. Direct injection creates a cycle once
        // the notification service is also injected into VMs that MainWindowViewModel
        // resolves eagerly: ScanViewModel → IAppNotificationService → INavigationService
        // → MainWindowViewModel (already mid-construction) → silent DI hang.
        // Lazy resolution breaks that — navigation only fires when the user
        // actually clicks a toast, well after construction.
        private readonly IServiceProvider _services;
        private WindowNotificationManager? _manager;

        public AvaloniaAppNotificationService(IServiceProvider services)
        {
            _services = services;
        }

        private INavigationService Navigation => _services.GetRequiredService<INavigationService>();

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
                    expiration: DefaultExpiration,
                    onClick: () => _ = Navigation.NavigateToBulkScanAsync()));
            });
        }

        public void NotifyPaymentRequired(string modelId, string? message)
        {
            if (_manager == null) return;

            Dispatcher.UIThread.Post(() =>
            {
                var body = string.IsNullOrWhiteSpace(message)
                    ? $"OpenRouter rejected '{modelId}' — your credit balance is negative. Add credits at openrouter.ai. Click to open Settings."
                    : $"{message.Trim()} (model: {modelId}). Click to open Settings.";

                // expiration: TimeSpan.Zero = sticky, no auto-dismiss. The user
                // must explicitly close the toast — billing problems shouldn't
                // disappear on their own.
                _manager.Show(new Notification(
                    "💳 Payment required",
                    body,
                    NotificationType.Error,
                    expiration: TimeSpan.Zero,
                    onClick: () => _ = Navigation.NavigateToSettingsAsync()));
            });
        }

        public void NotifyRateLimit(string modelId, RateLimitScope scope, int? retryAfterSeconds)
        {
            if (_manager == null) return;

            Dispatcher.UIThread.Post(() =>
            {
                var title = scope switch
                {
                    RateLimitScope.AccountPerDay => "⏱ Daily quota hit",
                    RateLimitScope.AccountPerMinute => "⏱ Per-minute rate limit",
                    RateLimitScope.ProviderUpstream => "⏱ Provider throttling",
                    _ => "⏱ Rate limit",
                };
                var retryHint = retryAfterSeconds is int s
                    ? $" Retry in ~{s}s."
                    : string.Empty;
                var body = $"OpenRouter rate-limited '{modelId}'.{retryHint} Click to open Settings.";

                _manager.Show(new Notification(
                    title,
                    body,
                    NotificationType.Warning,
                    expiration: DefaultExpiration,
                    onClick: () => _ = Navigation.NavigateToSettingsAsync()));
            });
        }

        public void NotifyFreeModelsExhausted(int failedCount)
        {
            if (_manager == null) return;

            Dispatcher.UIThread.Post(() =>
            {
                var body = failedCount > 1
                    ? $"All free models are exhausted on {failedCount} card(s). Switch to a paid model? Click to open Settings."
                    : "All free models are exhausted. Switch to a paid model? Click to open Settings.";

                _manager.Show(new Notification(
                    "Free models exhausted",
                    body,
                    NotificationType.Information,
                    expiration: DefaultExpiration,
                    onClick: () => _ = Navigation.NavigateToSettingsAsync()));
            });
        }
    }
}

using System.Collections.ObjectModel;
using Proxima.App.ViewModels;

namespace Proxima.App.Notifications;

public sealed class NoOpAppNotificationCenter : IAppNotificationCenter
{
    public ObservableCollection<AppNotificationViewModel> Toasts { get; } = [];

    public event EventHandler? NotificationsChanged
    {
        add { }
        remove { }
    }

    public Task NotifyAsync(AppNotificationLevel level, string title, string message, string? source = null, bool persist = true, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AppNotificationViewModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AppNotificationViewModel>>(Array.Empty<AppNotificationViewModel>());
    }

    public Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

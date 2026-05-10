using System.Collections.ObjectModel;
using Proxima.App.ViewModels;

namespace Proxima.App.Notifications;

public interface IAppNotificationCenter
{
    ObservableCollection<AppNotificationViewModel> Toasts { get; }

    event EventHandler? NotificationsChanged;

    Task NotifyAsync(
        AppNotificationLevel level,
        string title,
        string message,
        string? source = null,
        bool persist = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppNotificationViewModel>> ListAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

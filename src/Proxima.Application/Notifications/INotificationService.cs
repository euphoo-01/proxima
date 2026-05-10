using Proxima.Domain.Notifications;

namespace Proxima.Application.Notifications;

public interface INotificationService
{
    Task<UserNotification> CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserNotification>> ListActiveAsync(Guid userId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default);
}

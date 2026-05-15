using Proxima.Core.Domain.Notifications;

namespace Proxima.Core.Application.Notifications;

public interface INotificationRepository
{
    Task<IReadOnlyList<UserNotification>> ListActiveAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAsync(UserNotification notification, CancellationToken cancellationToken);

    Task MarkDeletedAsync(Guid userId, Guid notificationId, DateTimeOffset deletedAtUtc, CancellationToken cancellationToken);

    Task MarkAllDeletedAsync(Guid userId, DateTimeOffset deletedAtUtc, CancellationToken cancellationToken);
}

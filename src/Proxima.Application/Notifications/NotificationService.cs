using Proxima.Domain.Notifications;

namespace Proxima.Application.Notifications;

public sealed class NotificationService(INotificationRepository repository) : INotificationService
{
    private const int MaxTitleLength = 120;
    private const int MaxMessageLength = 2_000;
    private const int MaxSourceLength = 120;

    public async Task<UserNotification> CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.UserId == Guid.Empty)
        {
            throw new ArgumentException("Notification user id cannot be empty.", nameof(request));
        }

        string message = Normalize(request.Message, MaxMessageLength);
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Notification message cannot be empty.", nameof(request));
        }

        string title = Normalize(request.Title, MaxTitleLength);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = request.Severity switch
            {
                NotificationSeverity.Success => "Готово",
                NotificationSeverity.Warning => "Требует внимания",
                NotificationSeverity.Error => "Ошибка",
                _ => "Информация",
            };
        }

        UserNotification notification = new(
            Guid.NewGuid(),
            request.UserId,
            request.Severity,
            title,
            message,
            Normalize(request.Source, MaxSourceLength),
            DateTimeOffset.UtcNow,
            null);

        await repository.AddAsync(notification, cancellationToken).ConfigureAwait(false);
        return notification;
    }

    public Task<IReadOnlyList<UserNotification>> ListActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Task.FromResult<IReadOnlyList<UserNotification>>(Array.Empty<UserNotification>());
        }

        return repository.ListActiveAsync(userId, cancellationToken);
    }

    public Task DeleteAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || notificationId == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        return repository.MarkDeletedAsync(userId, notificationId, DateTimeOffset.UtcNow, cancellationToken);
    }

    public Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        return repository.MarkAllDeletedAsync(userId, DateTimeOffset.UtcNow, cancellationToken);
    }

    private static string Normalize(string? value, int maxLength)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}

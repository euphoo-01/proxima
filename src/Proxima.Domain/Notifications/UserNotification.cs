namespace Proxima.Domain.Notifications;

public sealed record UserNotification(
    Guid Id,
    Guid UserId,
    NotificationSeverity Severity,
    string Title,
    string Message,
    string Source,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DeletedAtUtc)
{
    public bool IsDeleted => DeletedAtUtc is not null;
}

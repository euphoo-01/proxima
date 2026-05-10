using Proxima.Domain.Notifications;

namespace Proxima.Application.Notifications;

public sealed record CreateNotificationRequest(
    Guid UserId,
    NotificationSeverity Severity,
    string Title,
    string Message,
    string? Source = null);

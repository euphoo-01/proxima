using Proxima.Core.Domain.Notifications;

namespace Proxima.Core.Application.Notifications;

public sealed record CreateNotificationRequest(
    Guid UserId,
    NotificationSeverity Severity,
    string Title,
    string Message,
    string? Source = null);

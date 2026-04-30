namespace Proxima.App.ViewModels;

public enum AppNotificationLevel
{
    Info,
    Success,
    Warning,
    Error,
}

public sealed record AppNotificationViewModel(Guid Id, AppNotificationLevel Level, string Message, DateTimeOffset CreatedAtUtc);

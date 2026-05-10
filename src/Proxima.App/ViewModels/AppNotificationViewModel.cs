using System.Globalization;
using Proxima.Domain.Notifications;

namespace Proxima.App.ViewModels;

public enum AppNotificationLevel
{
    Info,
    Success,
    Warning,
    Error,
}

public sealed class AppNotificationViewModel : ViewModelBase
{
    public AppNotificationViewModel(
        Guid id,
        AppNotificationLevel level,
        string title,
        string message,
        string source,
        DateTimeOffset createdAtUtc,
        bool isPersistent)
    {
        Id = id;
        Level = level;
        Title = string.IsNullOrWhiteSpace(title) ? LevelText : title.Trim();
        Message = message.Trim();
        Source = source.Trim();
        CreatedAtUtc = createdAtUtc;
        IsPersistent = isPersistent;
    }

    public Guid Id { get; }

    public AppNotificationLevel Level { get; }

    public string Title { get; }

    public string Message { get; }

    public string Source { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public bool IsPersistent { get; }

    public string LevelText => Level switch
    {
        AppNotificationLevel.Success => "Успех",
        AppNotificationLevel.Warning => "Внимание",
        AppNotificationLevel.Error => "Ошибка",
        _ => "Информация",
    };

    public string Icon => Level switch
    {
        AppNotificationLevel.Success => "✓",
        AppNotificationLevel.Warning => "!",
        AppNotificationLevel.Error => "×",
        _ => "i",
    };

    public string AccentBrushKey => Level switch
    {
        AppNotificationLevel.Success => "#19A974",
        AppNotificationLevel.Warning => "#D98E04",
        AppNotificationLevel.Error => "#E5484D",
        _ => "#2D6CDF",
    };

    public string SoftBackground => Level switch
    {
        AppNotificationLevel.Success => "#EAFBF4",
        AppNotificationLevel.Warning => "#FFF7E6",
        AppNotificationLevel.Error => "#FFF0F0",
        _ => "#EEF5FF",
    };

    public string CreatedAtText => CreatedAtUtc
        .ToLocalTime()
        .ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU"));

    public string SourceText => string.IsNullOrWhiteSpace(Source) ? "Proxima" : Source;

    public static AppNotificationViewModel FromDomain(UserNotification notification)
    {
        return new AppNotificationViewModel(
            notification.Id,
            FromDomainSeverity(notification.Severity),
            notification.Title,
            notification.Message,
            notification.Source,
            notification.CreatedAtUtc,
            isPersistent: true);
    }

    public static NotificationSeverity ToDomainSeverity(AppNotificationLevel level)
    {
        return level switch
        {
            AppNotificationLevel.Success => NotificationSeverity.Success,
            AppNotificationLevel.Warning => NotificationSeverity.Warning,
            AppNotificationLevel.Error => NotificationSeverity.Error,
            _ => NotificationSeverity.Info,
        };
    }

    public static AppNotificationLevel FromDomainSeverity(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Success => AppNotificationLevel.Success,
            NotificationSeverity.Warning => AppNotificationLevel.Warning,
            NotificationSeverity.Error => AppNotificationLevel.Error,
            _ => AppNotificationLevel.Info,
        };
    }
}

using Microsoft.Extensions.DependencyInjection;
using Proxima.Core.Application.Notifications;

namespace Proxima.Infrastructure.Notifications;

public static class NotificationComposition
{
    public static IServiceCollection AddNotificationModule(this IServiceCollection services)
    {
        services.AddSingleton<INotificationRepository, PostgresNotificationRepository>();
        services.AddSingleton<INotificationService, NotificationService>();
        return services;
    }
}

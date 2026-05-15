using Microsoft.Extensions.DependencyInjection;
using Proxima.Core.Application.Settings;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Settings;

public static class SettingsComposition
{
    public static IServiceCollection AddSettingsModule(this IServiceCollection services)
    {
        services.AddSingleton<IUserSettingsRepository, PostgresUserSettingsRepository>();
        services.AddSingleton<ISettingsService, SettingsService>();
        return services;
    }
}

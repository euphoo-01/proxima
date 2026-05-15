using Proxima.Core.Application.Settings;
using Proxima.Infrastructure.Persistence;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Settings;

public static class ProximaSettingsComposition
{
    public static ISettingsService CreateSettingsService(
        IProximaUnitOfWorkFactory uowFactory,
        IProximaUnitOfWorkAccessor uowAccessor)
    {
        return new SettingsService(new PostgresUserSettingsRepository(uowFactory, uowAccessor));
    }
}

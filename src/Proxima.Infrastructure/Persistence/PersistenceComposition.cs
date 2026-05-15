using Microsoft.Extensions.DependencyInjection;

namespace Proxima.Infrastructure.Persistence;

public static class PersistenceComposition
{
    public static IServiceCollection AddPersistenceModule(
        this IServiceCollection services,
        DatabaseOptions databaseOptions,
        DatabaseBootstrapService database)
    {
        services.AddSingleton(databaseOptions);
        services.AddSingleton(database);
        services.AddSingleton<IProximaUnitOfWorkAccessor, ProximaUnitOfWorkAccessor>();
        services.AddSingleton<IProximaUnitOfWorkFactory, ProximaUnitOfWorkFactory>();
        return services;
    }
}

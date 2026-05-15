using Microsoft.Extensions.DependencyInjection;
using Proxima.Core.Application.Assets;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Assets;

public static class AssetComposition
{
    public static IServiceCollection AddAssetModule(this IServiceCollection services)
    {
        services.AddSingleton<IAssetRepository, PostgresAssetRepository>();
        services.AddSingleton<IAssetService, AssetService>();
        return services;
    }
}

using Proxima.Core.Application.Assets;
using Proxima.Infrastructure.Persistence;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Assets;

public static class ProximaAssetComposition
{
    public static IAssetService CreateAssetService(
        IProximaUnitOfWorkFactory uowFactory,
        IProximaUnitOfWorkAccessor uowAccessor)
    {
        return new AssetService(new PostgresAssetRepository(uowFactory, uowAccessor));
    }
}

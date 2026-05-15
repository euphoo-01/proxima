using Proxima.Core.Application.Portfolios;
using Proxima.Infrastructure.Persistence;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Portfolios;

public static class ProximaPortfolioComposition
{
    public static IPortfolioService CreatePortfolioService(
        IProximaUnitOfWorkFactory uowFactory,
        IProximaUnitOfWorkAccessor uowAccessor)
    {
        return new PortfolioService(new PostgresPortfolioRepository(uowFactory, uowAccessor));
    }
}

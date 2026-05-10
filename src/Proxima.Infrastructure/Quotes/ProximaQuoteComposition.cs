using Proxima.Application.Quotes;
using Proxima.Infrastructure.Persistence;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Quotes;

public static class ProximaQuoteComposition
{
    public static IQuoteRefreshService CreateQuoteRefreshService(
        IProximaUnitOfWorkFactory uowFactory,
        IProximaUnitOfWorkAccessor uowAccessor,
        IQuoteProvider quoteProvider)
    {
        return new QuoteRefreshService(
            new PostgresAssetRepository(uowFactory, uowAccessor),
            quoteProvider,
            new PostgresQuoteCacheRepository(uowFactory, uowAccessor));
    }
}

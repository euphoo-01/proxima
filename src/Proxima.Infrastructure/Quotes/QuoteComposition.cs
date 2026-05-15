using Microsoft.Extensions.DependencyInjection;
using Proxima.Core.Application.MarketData;
using Proxima.Core.Application.Quotes;
using Proxima.Infrastructure.MarketData;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Quotes;

public static class QuoteComposition
{
    public static IServiceCollection AddQuoteModule(this IServiceCollection services)
    {
        services.AddSingleton<IQuoteCacheRepository, PostgresQuoteCacheRepository>();
        services.AddSingleton<IQuoteProvider, ConfigurableQuoteProvider>();
        services.AddSingleton<IMarketSymbolSearchService, TwelveDataSymbolSearchService>();
        services.AddSingleton<IQuoteRefreshService, QuoteRefreshService>();
        return services;
    }
}

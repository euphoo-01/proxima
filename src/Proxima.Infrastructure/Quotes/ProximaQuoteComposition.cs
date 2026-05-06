using Proxima.Application.Quotes;
using Proxima.Infrastructure.Assets;

namespace Proxima.Infrastructure.Quotes;

public static class ProximaQuoteComposition
{
    public static IQuoteRefreshService CreateQuoteRefreshService(
        string assetStorePath,
        string quoteCacheStorePath,
        string settingsStorePath)
    {
        return new QuoteRefreshService(
            new JsonAssetRepository(assetStorePath),
            new MockQuoteProvider(),
            new JsonQuoteCacheRepository(quoteCacheStorePath));
    }

    public static string GetDefaultQuoteCacheStorePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Proxima", "quote-cache.json");
    }
}

using Proxima.Application.Quotes;
using Proxima.Infrastructure.Assets;
using Proxima.Infrastructure.Settings;

namespace Proxima.Infrastructure.Quotes;

public static class ProximaQuoteComposition
{
    public static IQuoteRefreshService CreateQuoteRefreshService(string assetStorePath, string quoteCacheStorePath, string settingsStorePath)
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        return new QuoteRefreshService(
            new JsonAssetRepository(assetStorePath),
            new ConfigurableQuoteProvider(new LocalSettingsReader(settingsStorePath), client),
            new JsonQuoteCacheRepository(quoteCacheStorePath));
    }

    public static string GetDefaultQuoteCacheStorePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Proxima", "quote-cache.json");
    }
}

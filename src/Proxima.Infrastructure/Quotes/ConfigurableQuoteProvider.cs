using Proxima.Application.Quotes;
using Proxima.Application.Settings;
using Proxima.Infrastructure.Settings;

namespace Proxima.Infrastructure.Quotes;

public sealed class ConfigurableQuoteProvider(LocalSettingsReader settingsReader, HttpClient httpClient) : IQuoteProvider
{
    private readonly LocalSettingsReader _settingsReader = settingsReader;
    private readonly HttpClient _httpClient = httpClient;
    private readonly MockQuoteProvider _mock = new();

    public async Task<QuoteProviderResult> GetLatestQuoteAsync(string ticker, string currency, CancellationToken cancellationToken = default)
    {
        UserSettings? settings = await _settingsReader.TryReadPrimaryAsync(cancellationToken).ConfigureAwait(false);
        if (settings is null || settings.QuoteProvider == QuoteProviderKind.Mock)
        {
            return await _mock.GetLatestQuoteAsync(ticker, currency, cancellationToken).ConfigureAwait(false);
        }

        if (settings.QuoteProvider == QuoteProviderKind.Finnhub)
        {
            string key = LocalSettingsReader.UnprotectApiKey(settings.FinnhubApiKeyProtected);
            FinnhubQuoteProvider finnhub = new(_httpClient, key);
            return await finnhub.GetLatestQuoteAsync(ticker, currency, cancellationToken).ConfigureAwait(false);
        }

        return await _mock.GetLatestQuoteAsync(ticker, currency, cancellationToken).ConfigureAwait(false);
    }
}

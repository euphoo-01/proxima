using Proxima.Application.Settings;
using Proxima.Application.Taxes;
using Proxima.Infrastructure.Settings;

namespace Proxima.Infrastructure.Taxes;

public sealed class ConfigurableExchangeRateProvider(LocalSettingsReader settingsReader, HttpClient httpClient) : IExchangeRateProvider
{
    private readonly LocalSettingsReader _settingsReader = settingsReader;
    private readonly BelarusbankExchangeRateProvider _belarusbank = new(httpClient);
    private readonly MockNbrbExchangeRateProvider _mock = new();

    public async Task<ExchangeRateResult> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
    {
        UserSettings? settings = await _settingsReader.TryReadPrimaryAsync(cancellationToken).ConfigureAwait(false);
        if (settings is null || settings.CurrencyProvider == CurrencyProviderKind.Mock)
        {
            return await _mock.GetRateAsync(fromCurrency, toCurrency, date, cancellationToken).ConfigureAwait(false);
        }

        if (settings.CurrencyProvider == CurrencyProviderKind.Belarusbank)
        {
            ExchangeRateResult live = await _belarusbank.GetRateAsync(fromCurrency, toCurrency, date, cancellationToken).ConfigureAwait(false);
            return live.Succeeded
                ? live
                : await _mock.GetRateAsync(fromCurrency, toCurrency, date, cancellationToken).ConfigureAwait(false);
        }

        return await _mock.GetRateAsync(fromCurrency, toCurrency, date, cancellationToken).ConfigureAwait(false);
    }
}

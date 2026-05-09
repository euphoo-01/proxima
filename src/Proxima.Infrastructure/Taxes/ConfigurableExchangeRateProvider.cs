using Proxima.Application.Settings;
using Proxima.Application.Taxes;
using Proxima.Infrastructure.Settings;

namespace Proxima.Infrastructure.Taxes;

public sealed class ConfigurableExchangeRateProvider(LocalSettingsReader settingsReader, HttpClient httpClient) : IExchangeRateProvider
{
    private readonly LocalSettingsReader _settingsReader = settingsReader;
    private readonly NbrbExchangeRateProvider _nbrb = new(httpClient);
    private readonly BelarusbankExchangeRateProvider _belarusbank = new(httpClient);
    private readonly MockNbrbExchangeRateProvider _mock = new();

    public async Task<ExchangeRateResult> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
    {
        string from = Normalize(fromCurrency);
        string to = Normalize(toCurrency);
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return ExchangeRateResult.Success(1m, "BYN", date);
        }

        UserSettings? settings = await _settingsReader.TryReadPrimaryAsync(cancellationToken).ConfigureAwait(false);

        // В налоговом модуле Mock больше не является рабочим провайдером.
        // Старое значение настроек "Mock" трактуется как Auto: НБРБ -> Belarusbank -> аварийный fallback.
        bool preferBelarusbank = settings?.CurrencyProvider == CurrencyProviderKind.Belarusbank;

        ExchangeRateResult first = preferBelarusbank
            ? await _belarusbank.GetRateAsync(from, to, date, cancellationToken).ConfigureAwait(false)
            : await _nbrb.GetRateAsync(from, to, date, cancellationToken).ConfigureAwait(false);

        if (first.Succeeded)
        {
            return first;
        }

        ExchangeRateResult second = preferBelarusbank
            ? await _nbrb.GetRateAsync(from, to, date, cancellationToken).ConfigureAwait(false)
            : await _belarusbank.GetRateAsync(from, to, date, cancellationToken).ConfigureAwait(false);

        if (second.Succeeded)
        {
            return second;
        }

        ExchangeRateResult fallback = await _mock.GetRateAsync(from, to, date, cancellationToken).ConfigureAwait(false);
        return fallback with
        {
            Source = $"mock-fallback after official providers failed: {first.Message}; {second.Message}",
            Message = $"НБРБ и Belarusbank не вернули курс {from}->{to} за {date:dd.MM.yyyy}. Использован аварийный fallback.",
        };
    }

    private static string Normalize(string currency) => string.IsNullOrWhiteSpace(currency)
        ? "BYN"
        : currency.Trim().ToUpperInvariant();
}

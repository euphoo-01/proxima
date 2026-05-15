using Proxima.Core.Application.Auth;
using Proxima.Core.Application.Settings;
using Proxima.Core.Application.Taxes;

namespace Proxima.Infrastructure.Taxes;

public sealed class ConfigurableExchangeRateProvider(
    ICurrentUserContext currentUser,
    ISettingsService settingsService,
    HttpClient httpClient) : IExchangeRateProvider
{
    private readonly NbrbExchangeRateProvider _nbrb = new(httpClient);
    private readonly BelarusbankExchangeRateProvider _belarusbank = new(httpClient);
    private readonly MockNbrbExchangeRateProvider _mock = new();

    public async Task<ExchangeRateResult> GetRateAsync(
        string fromCurrency,
        string toCurrency,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        string from = Normalize(fromCurrency);
        string to = Normalize(toCurrency);
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return ExchangeRateResult.Success(1m, to, date);
        }

        CurrencyProviderKind providerKind = await ResolveProviderKindAsync(cancellationToken).ConfigureAwait(false);
        bool preferBelarusbank = providerKind == CurrencyProviderKind.Belarusbank;

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

    private async Task<CurrencyProviderKind> ResolveProviderKindAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
        {
            return CurrencyProviderKind.Mock;
        }

        UserSettings? settings = await settingsService
            .GetAsync(currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Старое значение Mock больше не выбирает mock-провайдер напрямую.
        // Оно трактуется как Auto: НБРБ -> Belarusbank -> аварийный fallback.
        return settings?.CurrencyProvider == CurrencyProviderKind.Belarusbank
            ? CurrencyProviderKind.Belarusbank
            : CurrencyProviderKind.Mock;
    }

    private static string Normalize(string currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? "BYN"
            : currency.Trim().ToUpperInvariant();
    }
}

using Proxima.Core.Application.Auth;
using Proxima.Core.Application.Settings;
using Proxima.Core.Application.Taxes;

namespace Proxima.Infrastructure.Taxes;

public sealed class ConfigurableExchangeRateProvider(
    ICurrentUserContext currentUser,
    ISettingsService settingsService,
    IEnumerable<IExchangeRateSource> sources) : IExchangeRateProvider
{
    private static readonly TimeSpan ProviderKindCacheTtl = TimeSpan.FromMinutes(5);

    private readonly IReadOnlyDictionary<CurrencyProviderKind, IExchangeRateSource> _sources = sources
        .GroupBy(source => source.Kind)
        .ToDictionary(group => group.Key, group => group.First());

    private Guid _cachedProviderUserId;
    private CurrencyProviderKind _cachedProviderKind;
    private DateTimeOffset _cachedProviderKindUntil;

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
        CurrencyProviderKind[] officialOrder = providerKind == CurrencyProviderKind.Belarusbank
            ? [CurrencyProviderKind.Belarusbank, CurrencyProviderKind.Nbrb]
            : [CurrencyProviderKind.Nbrb, CurrencyProviderKind.Belarusbank];

        List<ExchangeRateResult> failures = [];
        foreach (CurrencyProviderKind kind in officialOrder)
        {
            if (!_sources.TryGetValue(kind, out IExchangeRateSource? source))
            {
                continue;
            }

            ExchangeRateResult result = await source.GetRateAsync(from, to, date, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                return result;
            }

            failures.Add(result);
        }

        if (!_sources.TryGetValue(CurrencyProviderKind.Mock, out IExchangeRateSource? fallbackSource))
        {
            string failureMessage = string.Join("; ", failures.Select(item => item.Message));
            return ExchangeRateResult.Failure($"Ни один источник курсов не вернул курс {from}->{to} за {date:dd.MM.yyyy}: {failureMessage}");
        }

        ExchangeRateResult fallback = await fallbackSource.GetRateAsync(from, to, date, cancellationToken).ConfigureAwait(false);
        return fallback with
        {
            Source = $"mock-fallback after official providers failed: {string.Join("; ", failures.Select(item => item.Message))}",
            Message = $"НБРБ и Belarusbank не вернули курс {from}->{to} за {date:dd.MM.yyyy}. Использован аварийный fallback.",
        };
    }

    private async Task<CurrencyProviderKind> ResolveProviderKindAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
        {
            return CurrencyProviderKind.Mock;
        }

        Guid userId = currentUser.UserId;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (_cachedProviderUserId == userId && now < _cachedProviderKindUntil)
        {
            return _cachedProviderKind;
        }

        UserSettings? settings = await settingsService
            .GetAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        CurrencyProviderKind providerKind = settings?.CurrencyProvider switch
        {
            CurrencyProviderKind.Belarusbank => CurrencyProviderKind.Belarusbank,
            CurrencyProviderKind.Nbrb => CurrencyProviderKind.Nbrb,
            _ => CurrencyProviderKind.Nbrb,
        };

        _cachedProviderUserId = userId;
        _cachedProviderKind = providerKind;
        _cachedProviderKindUntil = now.Add(ProviderKindCacheTtl);
        return providerKind;
    }

    private static string Normalize(string currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? "BYN"
            : currency.Trim().ToUpperInvariant();
    }
}

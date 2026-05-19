using System.Collections.Concurrent;
using Proxima.Core.Application.Settings;
using Proxima.Core.Application.Taxes;

namespace Proxima.Infrastructure.Taxes;

public sealed class CachingExchangeRateSource(IExchangeRateSource inner) : IExchangeRateSource
{
    private readonly ConcurrentDictionary<string, ExchangeRateResult> _cache = new(StringComparer.OrdinalIgnoreCase);

    public CurrencyProviderKind Kind => inner.Kind;

    public async Task<ExchangeRateResult> GetRateAsync(
        string fromCurrency,
        string toCurrency,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        string from = Normalize(fromCurrency);
        string to = Normalize(toCurrency);
        string key = $"{Kind}:{from}:{to}:{date:yyyy-MM-dd}";

        if (_cache.TryGetValue(key, out ExchangeRateResult? cached))
        {
            return cached;
        }

        ExchangeRateResult result = await inner.GetRateAsync(from, to, date, cancellationToken).ConfigureAwait(false);
        if (result.Succeeded)
        {
            _cache[key] = result;
        }

        return result;
    }

    private static string Normalize(string currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? "BYN"
            : currency.Trim().ToUpperInvariant();
    }
}

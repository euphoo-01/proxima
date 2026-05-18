using Proxima.Core.Application.Settings;

namespace Proxima.Core.Application.Taxes;

public interface IExchangeRateSource
{
    CurrencyProviderKind Kind { get; }

    Task<ExchangeRateResult> GetRateAsync(
        string fromCurrency,
        string toCurrency,
        DateOnly date,
        CancellationToken cancellationToken = default);
}

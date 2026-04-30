using Proxima.Application.Taxes;

namespace Proxima.Infrastructure.Taxes;

public sealed class MockNbrbExchangeRateProvider : IExchangeRateProvider
{
    public Task<ExchangeRateResult> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(ExchangeRateResult.Success(1m, "mock-nbrb", date));
        }

        decimal rate = (Math.Abs((fromCurrency + toCurrency).GetHashCode()) % 250m) / 100m + 1m;
        return Task.FromResult(ExchangeRateResult.Success(rate, "mock-nbrb", date));
    }
}

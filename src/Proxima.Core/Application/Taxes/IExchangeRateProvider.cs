namespace Proxima.Core.Application.Taxes;

public interface IExchangeRateProvider
{
    Task<ExchangeRateResult> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default);
}

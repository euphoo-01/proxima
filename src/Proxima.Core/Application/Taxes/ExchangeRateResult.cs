namespace Proxima.Core.Application.Taxes;

public sealed record ExchangeRateResult(bool Succeeded, decimal Rate, string Source, DateOnly Date, string Message)
{
    public static ExchangeRateResult Success(decimal rate, string source, DateOnly date) => new(true, rate, source, date, string.Empty);
    public static ExchangeRateResult Failure(string message) => new(false, 0m, "unavailable", default, message);
}

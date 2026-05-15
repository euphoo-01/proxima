using Proxima.Core.Application.Quotes;

namespace Proxima.Infrastructure.Quotes;

public sealed class MockQuoteProvider : IQuoteProvider
{
    public Task<QuoteProviderResult> GetLatestQuoteAsync(string ticker, string currency, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ticker.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(QuoteProviderResult.Failure(QuoteProviderErrorKind.RateLimited, "Rate limit reached in mock provider."));
        }

        decimal seed = Math.Abs(ticker.ToUpperInvariant().GetHashCode()) % 5000;
        decimal price = 10m + seed / 10m;
        QuoteData quote = new(
            ticker.ToUpperInvariant(),
            price,
            currency.ToUpperInvariant(),
            DateTimeOffset.UtcNow,
            "mock",
            null,
            null);
        return Task.FromResult(QuoteProviderResult.Success(quote));
    }
}

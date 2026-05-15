namespace Proxima.Core.Application.Quotes;

public interface IQuoteProvider
{
    Task<QuoteProviderResult> GetLatestQuoteAsync(string ticker, string currency, CancellationToken cancellationToken = default);
}

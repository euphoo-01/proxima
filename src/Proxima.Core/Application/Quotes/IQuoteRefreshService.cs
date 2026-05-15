namespace Proxima.Core.Application.Quotes;

public interface IQuoteRefreshService
{
    Task<QuoteRefreshSummary> RefreshPortfolioAsync(Guid portfolioId, CancellationToken cancellationToken = default);
}

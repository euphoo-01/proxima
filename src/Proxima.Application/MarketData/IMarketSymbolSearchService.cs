namespace Proxima.Application.MarketData;

public interface IMarketSymbolSearchService
{
    Task<MarketSymbolSearchResult> SearchAsync(
        string query,
        int limit = 12,
        CancellationToken cancellationToken = default);

    Task<MarketSymbolSearchResult> ResolveAsync(
        string query,
        CancellationToken cancellationToken = default);
}

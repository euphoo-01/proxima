namespace Proxima.Core.Application.MarketData;

public sealed record MarketSymbolSearchResult(
    bool Succeeded,
    IReadOnlyList<MarketSymbolCandidate> Symbols,
    string Message)
{
    public static MarketSymbolSearchResult Success(IReadOnlyList<MarketSymbolCandidate> symbols)
    {
        return new MarketSymbolSearchResult(true, symbols, string.Empty);
    }

    public static MarketSymbolSearchResult Failure(string message)
    {
        return new MarketSymbolSearchResult(false, Array.Empty<MarketSymbolCandidate>(), message);
    }
}

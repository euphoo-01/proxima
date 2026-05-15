using Proxima.Core.Domain.Assets;

namespace Proxima.Core.Application.MarketData;

public sealed record MarketSymbolCandidate(
    string Symbol,
    string DisplaySymbol,
    string Description,
    string Type,
    string Currency,
    string Exchange,
    string? Mic,
    AssetType AssetType)
{
    public string PrimaryText => string.IsNullOrWhiteSpace(DisplaySymbol) ? Symbol : DisplaySymbol;

    public string SecondaryText
    {
        get
        {
            string description = string.IsNullOrWhiteSpace(Description) ? Type : Description;
            string exchange = string.IsNullOrWhiteSpace(Exchange) ? string.Empty : $" · {Exchange}";
            string currency = string.IsNullOrWhiteSpace(Currency) ? string.Empty : $" · {Currency}";
            return $"{description}{exchange}{currency}".Trim();
        }
    }

    public string SearchText => $"{Symbol} {DisplaySymbol} {Description} {Type} {Currency} {Exchange} {Mic}";
}

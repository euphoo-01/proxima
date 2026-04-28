using Proxima.Domain.Assets;

namespace Proxima.App.ViewModels;

public sealed record AssetRowViewModel(
    Guid Id,
    string Name,
    string Ticker,
    AssetType Type,
    string Currency,
    decimal Quantity,
    decimal AverageBuyPrice,
    decimal CurrentPrice,
    decimal Value,
    decimal ProfitLoss,
    IReadOnlyList<string> Tags)
{
    public string TypeLabel => Type switch
    {
        AssetType.Stock => "Stock",
        AssetType.Crypto => "Crypto",
        AssetType.Currency => "Currency",
        AssetType.Cash => "Cash",
        AssetType.Bond => "Bond",
        AssetType.Etf => "ETF",
        _ => "Other",
    };

    public string TagsText => Tags.Count == 0 ? "—" : string.Join(", ", Tags);
}

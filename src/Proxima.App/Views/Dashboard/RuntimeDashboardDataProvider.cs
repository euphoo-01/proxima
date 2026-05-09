using Proxima.Analytics.Dashboard;
using Proxima.App.Shell;
using Proxima.Application.Assets;
using Proxima.Application.Quotes;
using Proxima.Application.Transactions;
using Proxima.Domain.Assets;
using Proxima.Domain.Transactions;

namespace Proxima.App.Views.Dashboard;

public sealed class RuntimeDashboardDataProvider(
    IAssetService assetService,
    ITransactionService transactionService,
    IQuoteCacheRepository quoteCacheRepository,
    IShellState shellState)
    : IDashboardDataProvider
{
    public DashboardSnapshot GetSnapshot()
    {
        return GetSnapshotAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        Guid portfolioId = shellState.CurrentPortfolioId;
        if (portfolioId == Guid.Empty)
        {
            return DashboardSnapshot.Empty("USD");
        }

        IReadOnlyList<Asset> assets = await assetService.ListActiveAsync(portfolioId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<PortfolioTransaction> transactions = await transactionService.ListActiveAsync(portfolioId, cancellationToken).ConfigureAwait(false);

        Dictionary<Guid, Asset> assetById = assets.ToDictionary(static asset => asset.Id, static asset => asset);
        List<DashboardQuoteSnapshot> previousQuotes = [];

        foreach (Asset asset in assets)
        {
            QuoteCacheEntry? quote = await quoteCacheRepository.FindLatestByAssetIdAsync(asset.Id, cancellationToken).ConfigureAwait(false);
            if (quote is not null)
            {
                previousQuotes.Add(new DashboardQuoteSnapshot(asset.Id, quote.Price, quote.Timestamp));
            }
        }

        DashboardAssetSnapshot[] assetSnapshots = assets
            .Select(asset => new DashboardAssetSnapshot(
                asset.Id,
                string.IsNullOrWhiteSpace(asset.Name) ? asset.Ticker : asset.Name,
                MapAssetType(asset.Type),
                asset.Quantity,
                asset.CurrentPrice,
                Math.Max(0m, asset.Quantity * asset.CurrentPrice),
                NormalizeTags(asset)))
            .ToArray();

        DashboardTransactionSnapshot[] transactionSnapshots = transactions
            .OrderByDescending(static tx => tx.TradeDate)
            .Select(tx => MapTransaction(tx, assetById))
            .ToArray();

        IReadOnlyList<decimal> series = BuildPortfolioSeries(assetSnapshots, transactions);
        string currency = assets.FirstOrDefault()?.Currency ?? "USD";

        return new DashboardSnapshot(currency, assetSnapshots, previousQuotes, transactionSnapshots, series);
    }

    private static DashboardTransactionSnapshot MapTransaction(PortfolioTransaction tx, IReadOnlyDictionary<Guid, Asset> assetById)
    {
        Asset? asset = tx.AssetId is null ? null : assetById.GetValueOrDefault(tx.AssetId.Value);
        string name = asset?.Name ?? tx.Type switch
        {
            TransactionType.Deposit => "Пополнение",
            TransactionType.Withdrawal => "Вывод средств",
            TransactionType.Fee => "Комиссия",
            TransactionType.Tax => "Налог",
            _ => "Операция"
        };

        string ticker = asset?.Ticker ?? tx.Currency;
        decimal signedAmount = ToSignedAmount(tx);

        return new DashboardTransactionSnapshot(
            tx.Id,
            name,
            ticker,
            ToRussianType(tx.Type),
            tx.TradeDate,
            tx.Price,
            signedAmount);
    }

    private static decimal ToSignedAmount(PortfolioTransaction tx)
    {
        decimal amount = tx.GrossAmount > 0m
            ? tx.GrossAmount
            : tx.Quantity * tx.Price;

        return tx.Type switch
        {
            TransactionType.Buy => -amount - tx.FeeAmount - tx.TaxAmount,
            TransactionType.Withdrawal => -amount - tx.FeeAmount - tx.TaxAmount,
            TransactionType.Fee => -amount,
            TransactionType.Tax => -amount,
            _ => amount - tx.FeeAmount - tx.TaxAmount
        };
    }

    private static IReadOnlyList<decimal> BuildPortfolioSeries(IReadOnlyList<DashboardAssetSnapshot> assets, IReadOnlyList<PortfolioTransaction> transactions)
    {
        decimal currentTotal = assets.Sum(static asset => asset.Value);
        if (currentTotal <= 0m)
        {
            return [];
        }

        DateTimeOffset today = DateTimeOffset.UtcNow.Date;
        DateTimeOffset start = today.AddDays(-29);
        PortfolioTransaction[] orderedTransactions = transactions
            .Where(static tx => tx.AssetId.HasValue)
            .OrderBy(static tx => tx.TradeDate)
            .ToArray();

        if (orderedTransactions.Length == 0)
        {
            return [currentTotal];
        }

        Dictionary<Guid, decimal> currentPriceByAsset = assets.ToDictionary(
            static asset => asset.AssetId,
            static asset => asset.Price > 0m ? asset.Price : 0m);

        decimal[] values = new decimal[30];
        for (int i = 0; i < values.Length; i++)
        {
            DateTimeOffset dayEnd = start.AddDays(i + 1).AddTicks(-1);
            Dictionary<Guid, decimal> quantities = new();

            foreach (PortfolioTransaction tx in orderedTransactions)
            {
                if (tx.TradeDate > dayEnd || tx.AssetId is null)
                {
                    continue;
                }

                Guid assetId = tx.AssetId.Value;
                decimal currentQuantity = quantities.GetValueOrDefault(assetId);
                quantities[assetId] = tx.Type switch
                {
                    TransactionType.Buy => currentQuantity + tx.Quantity,
                    TransactionType.Airdrop => currentQuantity + tx.Quantity,
                    TransactionType.StakingReward => currentQuantity + tx.Quantity,
                    TransactionType.Sell => Math.Max(0m, currentQuantity - tx.Quantity),
                    _ => currentQuantity
                };
            }

            decimal value = 0m;
            foreach ((Guid assetId, decimal quantity) in quantities)
            {
                if (quantity <= 0m || !currentPriceByAsset.TryGetValue(assetId, out decimal price) || price <= 0m)
                {
                    continue;
                }

                value += quantity * price;
            }

            values[i] = Math.Max(0m, value);
        }

        values[^1] = currentTotal;
        return values;
    }

    private static IReadOnlyList<string> NormalizeTags(Asset asset)
    {
        if (asset.Tags.Count > 0)
        {
            return asset.Tags
                .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                .Select(static tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return [MapAssetType(asset.Type)];
    }

    private static string MapAssetType(AssetType type)
    {
        return type switch
        {
            AssetType.Stock => "Акции",
            AssetType.Crypto => "Криптовалюта",
            AssetType.Cash => "Наличность",
            AssetType.Bond => "Облигации",
            AssetType.Etf => "ETF",
            _ => "Другое"
        };
    }

    private static string ToRussianType(TransactionType type)
    {
        return type switch
        {
            TransactionType.Buy => "Ордер покупки",
            TransactionType.Sell => "Ордер продажи",
            TransactionType.Dividend => "Дивиденд",
            TransactionType.Deposit => "Пополнение",
            TransactionType.Withdrawal => "Вывод средств",
            TransactionType.Fee => "Комиссия",
            TransactionType.Tax => "Налог",
            TransactionType.Transfer => "Перевод",
            TransactionType.Split => "Сплит",
            TransactionType.Airdrop => "Airdrop",
            TransactionType.StakingReward => "Стейкинг",
            _ => type.ToString()
        };
    }
}

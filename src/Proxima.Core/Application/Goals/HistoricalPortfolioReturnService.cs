using Proxima.Core.Application.Assets;
using Proxima.Core.Application.Transactions;
using Proxima.Core.Domain.Assets;
using Proxima.Core.Domain.Transactions;

namespace Proxima.Core.Application.Goals;

public interface IHistoricalPortfolioReturnService
{
    Task<HistoricalPortfolioReturn> CalculateAsync(Guid portfolioId, CancellationToken cancellationToken = default);
}

public sealed class HistoricalPortfolioReturnService(
    IAssetRepository assetRepository,
    ITransactionRepository transactionRepository) : IHistoricalPortfolioReturnService
{
    public const decimal DefaultFallbackAnnualReturnPercent = 8m;

    private const decimal MinimumAnnualReturnPercent = -50m;
    private const decimal MaximumAnnualReturnPercent = 100m;
    private const int MinimumAnnualizationDays = 30;
    private const double DaysPerYear = 365.2425d;

    public async Task<HistoricalPortfolioReturn> CalculateAsync(Guid portfolioId, CancellationToken cancellationToken = default)
    {
        if (portfolioId == Guid.Empty)
        {
            return HistoricalPortfolioReturn.Fallback(
                DefaultFallbackAnnualReturnPercent,
                "Профиль не выбран, временно используется базовый сценарий 8% годовых.");
        }

        IReadOnlyList<Asset> assets = await assetRepository
            .ListByPortfolioAsync(portfolioId, includeArchived: false, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PortfolioTransaction> transactions = await transactionRepository
            .ListByPortfolioAsync(portfolioId, includeArchived: false, cancellationToken)
            .ConfigureAwait(false);

        decimal currentValue = CalculateCurrentValue(assets);
        decimal costBasis = CalculateAssetCostBasis(assets);
        if (costBasis <= 0m)
        {
            costBasis = CalculateTransactionCostBasis(transactions);
        }

        DateTimeOffset? startDate = ResolveStartDate(assets, transactions);
        int observationDays = CalculateObservationDays(startDate);

        if (currentValue <= 0m || costBasis <= 0m)
        {
            return HistoricalPortfolioReturn.Fallback(
                DefaultFallbackAnnualReturnPercent,
                "Недостаточно истории портфеля, временно используется базовый сценарий 8% годовых.",
                currentValue,
                costBasis,
                observationDays,
                startDate);
        }

        decimal totalReturnPercent = (currentValue - costBasis) / costBasis * 100m;
        decimal annualizedReturnPercent = AnnualizeReturn(currentValue, costBasis, observationDays);
        decimal normalizedAnnualReturnPercent = Math.Clamp(
            annualizedReturnPercent,
            MinimumAnnualReturnPercent,
            MaximumAnnualReturnPercent);

        string message = BuildMessage(totalReturnPercent, observationDays, annualizedReturnPercent, normalizedAnnualReturnPercent);

        return new HistoricalPortfolioReturn(
            normalizedAnnualReturnPercent,
            totalReturnPercent,
            currentValue,
            costBasis,
            observationDays,
            startDate,
            IsFallback: false,
            message);
    }

    private static decimal CalculateCurrentValue(IEnumerable<Asset> assets)
    {
        return assets
            .Where(static asset => !asset.IsArchived && asset.Quantity > 0m)
            .Sum(static asset => asset.Quantity * ResolveCurrentPrice(asset));
    }

    private static decimal CalculateAssetCostBasis(IEnumerable<Asset> assets)
    {
        return assets
            .Where(static asset => !asset.IsArchived && asset.Quantity > 0m && asset.AverageBuyPrice > 0m)
            .Sum(static asset => asset.Quantity * asset.AverageBuyPrice);
    }

    private static decimal CalculateTransactionCostBasis(IEnumerable<PortfolioTransaction> transactions)
    {
        decimal costBasis = 0m;
        foreach (PortfolioTransaction transaction in transactions.Where(static item => !item.IsArchived))
        {
            decimal grossAmount = ResolveGrossAmount(transaction);
            costBasis += transaction.Type switch
            {
                TransactionType.Buy => grossAmount + transaction.FeeAmount + transaction.TaxAmount,
                TransactionType.Sell => -grossAmount + transaction.FeeAmount + transaction.TaxAmount,
                TransactionType.Deposit => grossAmount,
                TransactionType.Withdrawal => -grossAmount,
                TransactionType.Dividend => -grossAmount,
                TransactionType.Fee => transaction.FeeAmount > 0m ? transaction.FeeAmount : grossAmount,
                TransactionType.Tax => transaction.TaxAmount > 0m ? transaction.TaxAmount : grossAmount,
                _ => 0m,
            };
        }

        return Math.Max(0m, costBasis);
    }

    private static DateTimeOffset? ResolveStartDate(IReadOnlyList<Asset> assets, IReadOnlyList<PortfolioTransaction> transactions)
    {
        DateTimeOffset? firstTransactionDate = transactions
            .Where(static transaction => !transaction.IsArchived)
            .Select(static transaction => (DateTimeOffset?)transaction.TradeDate)
            .OrderBy(static value => value)
            .FirstOrDefault();

        if (firstTransactionDate is not null)
        {
            return firstTransactionDate;
        }

        return assets
            .Where(static asset => !asset.IsArchived)
            .Select(static asset => (DateTimeOffset?)asset.CreatedAt)
            .OrderBy(static value => value)
            .FirstOrDefault();
    }

    private static int CalculateObservationDays(DateTimeOffset? startDate)
    {
        if (startDate is null)
        {
            return 0;
        }

        double totalDays = (DateTimeOffset.UtcNow - startDate.Value.ToUniversalTime()).TotalDays;
        return Math.Max(1, (int)Math.Floor(totalDays));
    }

    private static decimal AnnualizeReturn(decimal currentValue, decimal costBasis, int observationDays)
    {
        int effectiveDays = Math.Max(MinimumAnnualizationDays, observationDays);
        double growthRatio = (double)(currentValue / costBasis);
        double years = effectiveDays / DaysPerYear;
        double annualized = Math.Pow(growthRatio, 1d / years) - 1d;

        if (double.IsNaN(annualized) || double.IsInfinity(annualized))
        {
            return DefaultFallbackAnnualReturnPercent;
        }

        return (decimal)(annualized * 100d);
    }

    private static decimal ResolveCurrentPrice(Asset asset)
    {
        if (asset.CurrentPrice > 0m)
        {
            return asset.CurrentPrice;
        }

        return Math.Max(0m, asset.AverageBuyPrice);
    }

    private static decimal ResolveGrossAmount(PortfolioTransaction transaction)
    {
        if (transaction.GrossAmount > 0m)
        {
            return transaction.GrossAmount;
        }

        return Math.Max(0m, transaction.Quantity * transaction.Price);
    }

    private static string BuildMessage(
        decimal totalReturnPercent,
        int observationDays,
        decimal annualizedReturnPercent,
        decimal normalizedAnnualReturnPercent)
    {
        string prefix = observationDays < MinimumAnnualizationDays
            ? $"Истории меньше {MinimumAnnualizationDays} дней; годовой темп рассчитан осторожно."
            : $"CAGR по текущей истории портфеля за {observationDays:N0} дн.";

        string clampNote = annualizedReturnPercent == normalizedAnnualReturnPercent
            ? string.Empty
            : " Значение ограничено для устойчивого прогноза.";

        return $"{prefix} ROI: {totalReturnPercent:+0.#;-0.#;0}%.{clampNote}";
    }
}

public sealed record HistoricalPortfolioReturn(
    decimal? AnnualizedReturnPercent,
    decimal? TotalReturnPercent,
    decimal CurrentValue,
    decimal CostBasis,
    int ObservationDays,
    DateTimeOffset? StartDate,
    bool IsFallback,
    string Message)
{
    public static HistoricalPortfolioReturn Fallback(
        decimal fallbackAnnualReturnPercent,
        string message,
        decimal currentValue = 0m,
        decimal costBasis = 0m,
        int observationDays = 0,
        DateTimeOffset? startDate = null)
    {
        return new HistoricalPortfolioReturn(
            fallbackAnnualReturnPercent,
            null,
            currentValue,
            costBasis,
            observationDays,
            startDate,
            IsFallback: true,
            message);
    }
}

using Proxima.Analytics;
using Proxima.Analytics.AssetDetails;
using Proxima.Analytics.Dashboard;
using Proxima.Analytics.Engine;

namespace Proxima.Analytics.Tests;

internal static class Program
{
    private static void Main()
    {
        string? assemblyName = typeof(AnalyticsAssemblyMarker).Assembly.GetName().Name;
        Assert(assemblyName == "Proxima.Analytics", "Analytics assembly name must be Proxima.Analytics.");
        DashboardCalculator_TotalValueAndAllocation();
        DashboardCalculator_LatestTransactionsSortAndSearch();
        AssetDetailsCalculator_CoreMetrics();
        PortfolioAnalyticsEngine_CoreCalculations();
        Console.WriteLine("Proxima.Analytics.Tests passed.");
    }

    private static void PortfolioAnalyticsEngine_CoreCalculations()
    {
        PositionInput[] positions =
        [
            new(2m, 100m, 120m, 1m),
            new(1m, 50m, 70m, 0m),
        ];
        decimal total = PortfolioAnalyticsEngine.TotalValue(positions, cash: 10m);
        Assert(total == 320m, "Total value should include cash.");

        decimal pnl = PortfolioAnalyticsEngine.UnrealizedPnl(positions[0]);
        Assert(pnl == 39m, "PnL should include fees.");

        decimal? roi = PortfolioAnalyticsEngine.Roi(100m, pnl);
        Assert(roi is not null && roi > 0m, "ROI should compute for non-zero invested.");
        Assert(PortfolioAnalyticsEngine.Roi(0m, 10m) is null, "ROI should be unavailable for zero denominator.");

        decimal avgAfterBuy = PortfolioAnalyticsEngine.AverageCostAfterBuy(2m, 100m, 1m, 130m, 2m);
        Assert(avgAfterBuy > 100m, "Average cost should increase after higher buy with fees.");
        Assert(PortfolioAnalyticsEngine.AverageCostAfterSell(3m, avgAfterBuy, 1m) == avgAfterBuy, "Sell keeps average cost for remaining units.");

        double[] returns = [0.01, -0.02, 0.015, -0.005, 0.02];
        Assert(PortfolioAnalyticsEngine.Volatility(returns).Availability == MetricAvailability.Available, "Volatility should be available.");
        Assert(PortfolioAnalyticsEngine.MaxDrawdown([100d, 95d, 110d, 90d]).Value > 0d, "MDD should compute.");
        Assert(PortfolioAnalyticsEngine.Sharpe(returns).Availability == MetricAvailability.Available, "Sharpe should compute.");
        Assert(PortfolioAnalyticsEngine.Sortino(returns).Availability == MetricAvailability.Available, "Sortino should compute.");
        Assert(PortfolioAnalyticsEngine.VaR(returns).Availability == MetricAvailability.Available, "VaR should compute.");
        Assert(PortfolioAnalyticsEngine.CVaR(returns).Availability == MetricAvailability.Available, "CVaR should compute.");
        Assert(PortfolioAnalyticsEngine.Correlation(returns, returns.Select(v => v * 0.8d).ToArray()).Availability == MetricAvailability.Available, "Correlation should compute.");
    }

    private static void AssetDetailsCalculator_CoreMetrics()
    {
        List<decimal> closes = Enumerable.Range(1, 260).Select(i => 100m + i).ToList();
        List<decimal> highs = closes.Select(v => v + 2m).ToList();
        List<decimal> lows = closes.Select(v => v - 2m).ToList();

        decimal? sma50 = AssetDetailsCalculator.Sma(closes, 50);
        decimal? rsi = AssetDetailsCalculator.Rsi(closes, 14);
        decimal? atr = AssetDetailsCalculator.Atr(highs, lows, closes, 14);
        Assert(sma50 is not null && sma50 > 0m, "SMA should be computed for sufficient series.");
        Assert(rsi is not null, "RSI should be computed for sufficient series.");
        Assert(atr is not null && atr > 0m, "ATR should be computed for sufficient series.");

        IReadOnlyList<double> returns = AssetDetailsCalculator.Returns(closes);
        Assert(AssetDetailsCalculator.Sharpe(returns) is not null, "Sharpe should compute for valid returns.");
        Assert(AssetDetailsCalculator.Sortino(returns) is not null, "Sortino should compute for valid returns.");
        Assert(AssetDetailsCalculator.VaR(returns, 0.95) is not null, "VaR should compute.");
        Assert(AssetDetailsCalculator.CVaR(returns, 0.95) is not null, "CVaR should compute.");
        Assert(AssetDetailsCalculator.ZScore(closes.Select(v => (double)v).ToArray()) is not null, "Z-Score should compute.");

        double[] benchmark = returns.Select(v => v * 0.8d).ToArray();
        Assert(AssetDetailsCalculator.Beta(returns, benchmark) is not null, "Beta should compute with benchmark.");
        Assert(AssetDetailsCalculator.Correlation(returns, benchmark) is not null, "Correlation should compute with benchmark.");
    }

    private static void DashboardCalculator_TotalValueAndAllocation()
    {
        DashboardAssetSnapshot[] assets =
        [
            new(Guid.NewGuid(), "Apple", "AAPL", 2m, 100m, 200m, ["tech"]),
            new(Guid.NewGuid(), "Bond", "BND", 1m, 150m, 150m, []),
        ];

        decimal total = PortfolioDashboardCalculator.CalculateTotalValue(assets);
        Assert(total == 350m, "Total value must sum asset values.");

        IReadOnlyList<AllocationSlice> alloc = PortfolioDashboardCalculator.BuildAllocationByTag(assets);
        Assert(alloc.Any(item => item.Tag == "tech"), "Allocation should include explicit tag.");
        Assert(alloc.Any(item => item.Tag == "Без категории"), "Allocation should include untagged bucket.");
    }

    private static void DashboardCalculator_LatestTransactionsSortAndSearch()
    {
        DashboardTransactionSnapshot[] transactions =
        [
            new(Guid.NewGuid(), "Apple", "AAPL", "Buy", DateTimeOffset.UtcNow.AddDays(-1), 100m, 200m),
            new(Guid.NewGuid(), "Bond", "BND", "Dividend", DateTimeOffset.UtcNow, 0m, 50m),
        ];

        IReadOnlyList<DashboardTransactionSnapshot> filtered = PortfolioDashboardCalculator.LatestTransactions(
            transactions, "Bond", "date_desc");
        Assert(filtered.Count == 1 && filtered[0].AssetName == "Bond", "Search must filter by asset.");

        IReadOnlyList<DashboardTransactionSnapshot> sorted = PortfolioDashboardCalculator.LatestTransactions(
            transactions, string.Empty, "price_desc");
        Assert(sorted[0].Price >= sorted[1].Price, "Sort by price must be descending.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

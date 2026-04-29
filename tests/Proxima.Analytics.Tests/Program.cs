using Proxima.Analytics;
using Proxima.Analytics.Dashboard;

namespace Proxima.Analytics.Tests;

internal static class Program
{
    private static void Main()
    {
        string? assemblyName = typeof(AnalyticsAssemblyMarker).Assembly.GetName().Name;
        Assert(assemblyName == "Proxima.Analytics", "Analytics assembly name must be Proxima.Analytics.");
        DashboardCalculator_TotalValueAndAllocation();
        DashboardCalculator_LatestTransactionsSortAndSearch();
        Console.WriteLine("Proxima.Analytics.Tests passed.");
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

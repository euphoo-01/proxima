namespace Proxima.Core.Application.Analytics.Dashboard;

public static class PortfolioDashboardCalculator
{
    public static decimal CalculateTotalValue(IEnumerable<DashboardAssetRow> assets)
    {
        return assets.Sum(static asset => asset.Value);
    }

    public static Delta24h Calculate24hDelta(IEnumerable<DashboardAssetRow> assets, IEnumerable<DashboardQuoteRow> previousQuotes)
    {
        Dictionary<Guid, DashboardQuoteRow> byAsset = previousQuotes.ToDictionary(static item => item.AssetId, static item => item);
        decimal currentTotal = 0m;
        decimal previousTotal = 0m;
        int seen = 0;

        foreach (DashboardAssetRow asset in assets)
        {
            currentTotal += asset.Value;
            if (!byAsset.TryGetValue(asset.AssetId, out DashboardQuoteRow? previous))
            {
                continue;
            }

            seen++;
            previousTotal += asset.Quantity * previous!.Price;
        }

        if (seen == 0)
        {
            return new Delta24h(0m, null, false);
        }

        decimal absolute = currentTotal - previousTotal;
        decimal? percent = previousTotal == 0m ? null : absolute / previousTotal * 100m;
        return new Delta24h(absolute, percent, true);
    }

    public static IReadOnlyList<AllocationSlice> BuildAllocationByTag(IEnumerable<DashboardAssetRow> assets)
    {
        return assets
            .SelectMany(static asset =>
            {
                IReadOnlyList<string> tags = asset.Tags.Count == 0 ? ["Без категории"] : asset.Tags;
                decimal perTag = tags.Count == 0 ? 0m : asset.Value / tags.Count;
                return tags.Select(tag => new AllocationSlice(tag, perTag));
            })
            .GroupBy(static item => item.Tag, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new AllocationSlice(group.Key, group.Sum(static item => item.Value)))
            .OrderByDescending(static item => item.Value)
            .ToArray();
    }

    public static IReadOnlyList<DashboardTransactionRow> LatestTransactions(
        IEnumerable<DashboardTransactionRow> transactions,
        string search,
        string sort,
        int take = 10)
    {
        IEnumerable<DashboardTransactionRow> query = transactions;
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(item =>
                item.AssetName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.Ticker.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.TypeLabel.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        query = sort switch
        {
            "name_asc" => query.OrderBy(static item => item.AssetName, StringComparer.OrdinalIgnoreCase),
            "price_desc" => query.OrderByDescending(static item => item.Price),
            "type_asc" => query.OrderBy(static item => item.TypeLabel, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderByDescending(static item => item.TradeDate),
        };

        return query.Take(take).ToArray();
    }

    public static IReadOnlyList<(DateTimeOffset Time, decimal Value)> BuildHistorySeries(
        IEnumerable<DashboardTransactionRow> transactions,
        string timeframe)
    {
        TimeSpan window = timeframe switch
        {
            "1D" => TimeSpan.FromDays(1),
            "7D" => TimeSpan.FromDays(7),
            _ => TimeSpan.FromDays(30),
        };

        DateTimeOffset from = DateTimeOffset.UtcNow - window;
        return transactions
            .Where(item => item.TradeDate >= from)
            .OrderBy(item => item.TradeDate)
            .Select(item => (item.TradeDate, item.GrossAmount))
            .ToArray();
    }
}

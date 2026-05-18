using Proxima.Core.Application.Analytics.Dashboard;

namespace Proxima.Core.Application.Dashboard;

public sealed record DashboardOverview(
    string Currency,
    IReadOnlyList<DashboardAssetRow> Assets,
    IReadOnlyList<DashboardQuoteRow> PreviousQuotes,
    IReadOnlyList<DashboardTransactionRow> Transactions,
    IReadOnlyList<decimal> FallbackSeries)
{
    public static DashboardOverview Empty(string currency) => new(currency, [], [], [], []);
}

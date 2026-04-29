namespace Proxima.Analytics.Engine;

public static class PortfolioAnalyticsEngine
{
    public static decimal TotalValue(IEnumerable<PositionInput> positions, decimal cash = 0m)
    {
        return positions.Sum(static p => p.Quantity * p.CurrentPrice) + cash;
    }

    public static decimal UnrealizedPnl(PositionInput position)
    {
        return position.Quantity * (position.CurrentPrice - position.AverageCost) - position.Fees;
    }

    public static decimal? Roi(decimal invested, decimal pnl)
    {
        if (invested == 0m)
        {
            return null;
        }

        return pnl / invested * 100m;
    }

    public static decimal AverageCostAfterBuy(decimal currentQty, decimal currentAvg, decimal buyQty, decimal buyPrice, decimal fees)
    {
        decimal totalQty = currentQty + buyQty;
        if (totalQty <= 0m)
        {
            return 0m;
        }

        decimal currentCost = currentQty * currentAvg;
        decimal buyCost = buyQty * buyPrice + fees;
        return (currentCost + buyCost) / totalQty;
    }

    public static decimal AverageCostAfterSell(decimal currentQty, decimal currentAvg, decimal sellQty)
    {
        decimal remaining = currentQty - sellQty;
        return remaining <= 0m ? 0m : currentAvg;
    }

    public static MetricResult Volatility(IReadOnlyList<double> returns)
    {
        if (returns.Count < 2)
        {
            return Unavailable("Volatility", "%", "Insufficient data.");
        }

        double mean = returns.Average();
        double std = Math.Sqrt(returns.Select(value => Math.Pow(value - mean, 2)).Average());
        return Available("Volatility", std * Math.Sqrt(252d) * 100d, "%", "Annualized std-dev.");
    }

    public static MetricResult MaxDrawdown(IReadOnlyList<double> prices)
    {
        if (prices.Count < 2)
        {
            return Unavailable("Max Drawdown", "%", "Insufficient data.");
        }

        double peak = prices[0];
        double maxDd = 0d;
        foreach (double price in prices)
        {
            if (price > peak)
            {
                peak = price;
            }

            if (peak <= 0d)
            {
                continue;
            }

            maxDd = Math.Max(maxDd, (peak - price) / peak);
        }

        return Available("Max Drawdown", maxDd * 100d, "%", "Peak-to-trough decline.");
    }

    public static MetricResult Sharpe(IReadOnlyList<double> returns, double riskFree = 0d)
    {
        if (returns.Count < 2)
        {
            return Unavailable("Sharpe", "", "Insufficient data.");
        }

        double mean = returns.Average() - riskFree;
        double std = Math.Sqrt(returns.Select(value => Math.Pow(value - returns.Average(), 2)).Average());
        if (std == 0d)
        {
            return Unavailable("Sharpe", "", "Zero volatility.");
        }

        return Available("Sharpe", mean / std * Math.Sqrt(252d), "", "Risk-adjusted return.");
    }

    public static MetricResult Sortino(IReadOnlyList<double> returns, double riskFree = 0d)
    {
        if (returns.Count < 2)
        {
            return Unavailable("Sortino", "", "Insufficient data.");
        }

        double downside = Math.Sqrt(returns.Where(static v => v < 0d).Select(static v => v * v).DefaultIfEmpty(0d).Average());
        if (downside == 0d)
        {
            return Unavailable("Sortino", "", "No downside deviation.");
        }

        return Available("Sortino", (returns.Average() - riskFree) / downside * Math.Sqrt(252d), "", "Downside-risk adjusted return.");
    }

    public static MetricResult VaR(IReadOnlyList<double> returns, double confidence = 0.95)
    {
        if (returns.Count == 0)
        {
            return Unavailable("VaR", "%", "Insufficient data.");
        }

        double[] ordered = returns.OrderBy(v => v).ToArray();
        int index = Math.Clamp((int)Math.Floor((1d - confidence) * ordered.Length), 0, ordered.Length - 1);
        return Available("VaR", ordered[index] * 100d, "%", "Historical quantile.");
    }

    public static MetricResult CVaR(IReadOnlyList<double> returns, double confidence = 0.95)
    {
        MetricResult var = VaR(returns, confidence);
        if (var.Availability == MetricAvailability.Unavailable || var.Value is null)
        {
            return Unavailable("CVaR", "%", "Insufficient data.");
        }

        double threshold = var.Value.Value / 100d;
        double[] tail = returns.Where(v => v <= threshold).ToArray();
        if (tail.Length == 0)
        {
            return Unavailable("CVaR", "%", "No tail observations.");
        }

        return Available("CVaR", tail.Average() * 100d, "%", "Average tail loss.");
    }

    public static MetricResult Correlation(IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        int length = Math.Min(first.Count, second.Count);
        if (length < 2)
        {
            return Unavailable("Correlation", "", "Insufficient data.");
        }

        double[] left = first.Take(length).ToArray();
        double[] right = second.Take(length).ToArray();
        double meanLeft = left.Average();
        double meanRight = right.Average();
        double cov = 0d;
        for (int i = 0; i < length; i++)
        {
            cov += (left[i] - meanLeft) * (right[i] - meanRight);
        }

        cov /= length;
        double stdLeft = Math.Sqrt(left.Select(v => Math.Pow(v - meanLeft, 2)).Average());
        double stdRight = Math.Sqrt(right.Select(v => Math.Pow(v - meanRight, 2)).Average());
        if (stdLeft == 0d || stdRight == 0d)
        {
            return Unavailable("Correlation", "", "Zero standard deviation.");
        }

        return Available("Correlation", cov / (stdLeft * stdRight), "", "Pearson correlation.");
    }

    private static MetricResult Available(string name, double value, string unit, string explanation)
    {
        return new MetricResult(name, value, unit, MetricAvailability.Available, explanation);
    }

    private static MetricResult Unavailable(string name, string unit, string explanation)
    {
        return new MetricResult(name, null, unit, MetricAvailability.Unavailable, explanation, "Unavailable");
    }
}

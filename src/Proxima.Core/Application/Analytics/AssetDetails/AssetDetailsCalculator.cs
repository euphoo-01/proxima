namespace Proxima.Core.Application.Analytics.AssetDetails;

public static class AssetDetailsCalculator
{
    public static AssetDetailsAnalyticsResult Calculate(
        IReadOnlyList<decimal> closes,
        IReadOnlyList<decimal> highs,
        IReadOnlyList<decimal> lows,
        decimal currentPrice,
        decimal? beta)
    {
        decimal[] normalizedCloses = closes.Where(static item => item > 0m).ToArray();
        decimal[] normalizedHighs = highs.Where(static item => item > 0m).ToArray();
        decimal[] normalizedLows = lows.Where(static item => item > 0m).ToArray();

        if (normalizedCloses.Length < 3)
        {
            return AssetDetailsAnalyticsResult.Empty(beta ?? 1m);
        }

        decimal[] returns = normalizedCloses
            .Zip(normalizedCloses.Skip(1), static (previous, current) => previous <= 0m ? 0m : (current - previous) / previous)
            .ToArray();

        decimal mean = returns.Average();
        decimal std = StandardDeviation(returns);
        decimal downside = StandardDeviation(returns.Where(static item => item < 0m).DefaultIfEmpty(0m).ToArray());
        decimal percentile5 = Percentile(returns, 0.05m);

        decimal maxDrawdown = MaxDrawdown(normalizedCloses) * 100m;
        decimal var = percentile5 * 100m;
        decimal cvar = returns
            .Where(item => item <= percentile5)
            .DefaultIfEmpty(0m)
            .Average() * 100m;

        decimal sharpe = std > 0m ? mean / std * Sqrt(252m) : 0m;
        decimal sortino = downside > 0m ? mean / downside * Sqrt(252m) : 0m;
        decimal annualReturn = mean * 252m;
        decimal calmar = maxDrawdown < 0m ? annualReturn / Math.Abs(maxDrawdown / 100m) : 0m;
        decimal atr = AverageTrueRange(normalizedHighs, normalizedLows, normalizedCloses);
        decimal rsi = Rsi(normalizedCloses);

        decimal smaShort = normalizedCloses.TakeLast(Math.Min(50, normalizedCloses.Length)).Average();
        decimal smaLong = normalizedCloses.TakeLast(Math.Min(200, normalizedCloses.Length)).Average();

        decimal averageClose = normalizedCloses.Average();
        decimal zScore = std > 0m && averageClose > 0m
            ? (currentPrice - averageClose) / (std * averageClose)
            : 0m;

        decimal trend = Math.Clamp(Math.Abs((smaShort - smaLong) / Math.Max(1m, smaLong)), 0m, 0.4m);
        decimal hurst = Math.Clamp(0.5m + trend, 0.35m, 0.9m);
        decimal correlation = Autocorrelation(returns);

        decimal spread = currentPrice > 0m
            ? atr / currentPrice * 0.18m * 100m
            : 0m;

        string smaStatus = smaShort >= smaLong ? "Золотой крест" : "Слабый тренд";
        string rsiHint = rsi >= 70m ? "Перекупленность" : rsi <= 30m ? "Перепроданность" : "Нейтрально";
        string atrHint = currentPrice > 0m && atr / currentPrice > 0.025m
            ? "Высокая волатильность"
            : "Умеренная волатильность";

        return new AssetDetailsAnalyticsResult(
            sharpe,
            sortino,
            calmar,
            maxDrawdown,
            var,
            cvar,
            rsi,
            atr,
            hurst,
            zScore,
            correlation,
            beta ?? 1m,
            spread,
            smaStatus,
            smaShort >= smaLong,
            rsiHint,
            atrHint);
    }

    private static decimal StandardDeviation(decimal[] values)
    {
        if (values.Length < 2)
        {
            return 0m;
        }

        decimal mean = values.Average();
        double variance = values.Select(item => Math.Pow((double)(item - mean), 2d)).Average();
        return (decimal)Math.Sqrt(variance);
    }

    private static decimal MaxDrawdown(decimal[] closes)
    {
        decimal peak = closes[0];
        decimal maxDrawdown = 0m;

        foreach (decimal close in closes)
        {
            if (close > peak)
            {
                peak = close;
            }

            if (peak <= 0m)
            {
                continue;
            }

            decimal drawdown = (close - peak) / peak;
            if (drawdown < maxDrawdown)
            {
                maxDrawdown = drawdown;
            }
        }

        return maxDrawdown;
    }

    private static decimal Percentile(decimal[] values, decimal p)
    {
        if (values.Length == 0)
        {
            return 0m;
        }

        decimal[] sorted = values.Order().ToArray();
        int index = Math.Clamp(
            (int)Math.Floor((double)(p * (sorted.Length - 1))),
            0,
            sorted.Length - 1);

        return sorted[index];
    }

    private static decimal AverageTrueRange(decimal[] highs, decimal[] lows, decimal[] closes)
    {
        int count = Math.Min(highs.Length, Math.Min(lows.Length, closes.Length));
        if (count == 0)
        {
            return 0m;
        }

        List<decimal> ranges = new(count);

        for (int i = 0; i < count; i++)
        {
            decimal previousClose = i == 0 ? closes[i] : closes[i - 1];
            decimal trueRange = Math.Max(
                highs[i] - lows[i],
                Math.Max(Math.Abs(highs[i] - previousClose), Math.Abs(lows[i] - previousClose)));

            ranges.Add(trueRange);
        }

        return ranges.TakeLast(Math.Min(14, ranges.Count)).Average();
    }

    private static decimal Rsi(decimal[] closes)
    {
        if (closes.Length < 3)
        {
            return 50m;
        }

        decimal[] deltas = closes
            .Zip(closes.Skip(1), static (previous, current) => current - previous)
            .TakeLast(14)
            .ToArray();

        decimal gains = deltas.Where(static item => item > 0m).DefaultIfEmpty(0m).Average();
        decimal losses = Math.Abs(deltas.Where(static item => item < 0m).DefaultIfEmpty(0m).Average());

        if (losses == 0m)
        {
            return 100m;
        }

        decimal rs = gains / losses;
        return 100m - 100m / (1m + rs);
    }

    private static decimal Autocorrelation(decimal[] returns)
    {
        if (returns.Length < 3)
        {
            return 0m;
        }

        decimal[] a = returns.Take(returns.Length - 1).ToArray();
        decimal[] b = returns.Skip(1).ToArray();

        decimal ma = a.Average();
        decimal mb = b.Average();

        decimal numerator = a.Zip(b, (x, y) => (x - ma) * (y - mb)).Sum();
        decimal da = a.Sum(x => (x - ma) * (x - ma));
        decimal db = b.Sum(y => (y - mb) * (y - mb));

        decimal denominator = Sqrt(da * db);

        return denominator > 0m ? numerator / denominator : 0m;
    }

    private static decimal Sqrt(decimal value) => value <= 0m ? 0m : (decimal)Math.Sqrt((double)value);
}

public sealed record AssetDetailsAnalyticsResult(
    decimal Sharpe,
    decimal Sortino,
    decimal Calmar,
    decimal MaxDrawdownPct,
    decimal VarPct,
    decimal CvarPct,
    decimal Rsi,
    decimal Atr,
    decimal Hurst,
    decimal ZScore,
    decimal Correlation,
    decimal Beta,
    decimal SpreadPct,
    string SmaStatus,
    bool IsSmaPositive,
    string RsiHint,
    string AtrHint)
{
    public static AssetDetailsAnalyticsResult Empty(decimal beta)
    {
        return new AssetDetailsAnalyticsResult(
            0m,
            0m,
            0m,
            0m,
            0m,
            0m,
            50m,
            0m,
            0.5m,
            0m,
            0m,
            beta,
            0m,
            "Недостаточно данных",
            false,
            "Недостаточно данных",
            "Недостаточно данных");
    }
}

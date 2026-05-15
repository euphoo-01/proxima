namespace Proxima.Core.Application.Analytics.AssetDetails;

public static class AssetDetailsCalculator
{
    public static IReadOnlyList<AssetMetric> BuildBaseMetrics(AssetDetailsSnapshot snapshot)
    {
        decimal? sma50 = Sma(snapshot.ClosePrices, 50);
        decimal? sma200 = Sma(snapshot.ClosePrices, 200);
        decimal? rsi = Rsi(snapshot.ClosePrices, 14);
        decimal? atr = Atr(snapshot.HighPrices, snapshot.LowPrices, snapshot.ClosePrices, 14);

        return
        [
            Metric("SMA 50", sma50, "price", "Средняя цена за 50 периодов."),
            Metric("SMA 200", sma200, "price", "Средняя цена за 200 периодов."),
            Metric("RSI", rsi, "", "Индекс относительной силы за 14 периодов."),
            Metric("ATR", atr, "price", "Средний истинный диапазон за 14 периодов."),
            Metric("24h Volume", (decimal?)null, "", "Объем 24h будет доступен после интеграции провайдера."),
            Metric("Market Cap", (decimal?)null, "", "Недоступно без внешнего источника."),
        ];
    }

    public static IReadOnlyList<AssetMetric> BuildAdvancedMetrics(AssetDetailsSnapshot snapshot)
    {
        IReadOnlyList<double> returns = Returns(snapshot.ClosePrices);
        double? sharpe = Sharpe(returns);
        double? sortino = Sortino(returns);
        double? maxDrawdown = MaxDrawdown(snapshot.ClosePrices.Select(static value => (double)value).ToArray());
        double? calmar = Calmar(returns, maxDrawdown);
        double? var95 = VaR(returns, 0.95);
        double? cvar95 = CVaR(returns, 0.95);
        double? zscore = ZScore(snapshot.ClosePrices.Select(static value => (double)value).ToArray());
        double? beta = null;
        double? corr = null;

        return
        [
            Metric("Sharpe", sharpe, "", "Доходность к волатильности."),
            Metric("Sortino", sortino, "", "Доходность к downside-риску."),
            Metric("Calmar", calmar, "", "Доходность к максимальной просадке."),
            Metric("Max Drawdown", maxDrawdown, "%", "Максимальная историческая просадка."),
            Metric("VaR 95%", var95, "%", "Оценка возможного убытка."),
            Metric("CVaR 95%", cvar95, "%", "Средний убыток в хвосте распределения."),
            Metric("Z-Score", zscore, "", "Стандартизованное отклонение текущей цены."),
            Metric("Beta", beta, "", "Требуется бенчмарк-ряд."),
            Metric("Correlation", corr, "", "Требуется второй временной ряд."),
        ];
    }

    public static decimal? Sma(IReadOnlyList<decimal> prices, int period)
    {
        if (period <= 0 || prices.Count < period)
        {
            return null;
        }

        decimal sum = 0m;
        for (int i = prices.Count - period; i < prices.Count; i++)
        {
            sum += prices[i];
        }

        return sum / period;
    }

    public static decimal? Rsi(IReadOnlyList<decimal> prices, int period)
    {
        if (period <= 0 || prices.Count <= period)
        {
            return null;
        }

        decimal gain = 0m;
        decimal loss = 0m;
        for (int i = prices.Count - period; i < prices.Count; i++)
        {
            decimal change = prices[i] - prices[i - 1];
            if (change >= 0)
            {
                gain += change;
            }
            else
            {
                loss += Math.Abs(change);
            }
        }

        if (loss == 0m)
        {
            return 100m;
        }

        decimal rs = gain / loss;
        return 100m - (100m / (1m + rs));
    }

    public static decimal? Atr(IReadOnlyList<decimal> highs, IReadOnlyList<decimal> lows, IReadOnlyList<decimal> closes, int period)
    {
        if (period <= 0 || highs.Count <= period || lows.Count <= period || closes.Count <= period)
        {
            return null;
        }

        decimal sumTr = 0m;
        for (int i = closes.Count - period; i < closes.Count; i++)
        {
            decimal highLow = highs[i] - lows[i];
            decimal highPrev = Math.Abs(highs[i] - closes[i - 1]);
            decimal lowPrev = Math.Abs(lows[i] - closes[i - 1]);
            sumTr += Math.Max(highLow, Math.Max(highPrev, lowPrev));
        }

        return sumTr / period;
    }

    public static double? Sharpe(IReadOnlyList<double> returns)
    {
        if (returns.Count < 2)
        {
            return null;
        }

        double avg = returns.Average();
        double std = StdDev(returns, avg);
        if (std == 0d)
        {
            return null;
        }

        return avg / std * Math.Sqrt(252);
    }

    public static double? Sortino(IReadOnlyList<double> returns)
    {
        if (returns.Count < 2)
        {
            return null;
        }

        double avg = returns.Average();
        double downside = Math.Sqrt(returns.Where(static value => value < 0d).Select(static value => value * value).DefaultIfEmpty(0d).Average());
        if (downside == 0d)
        {
            return null;
        }

        return avg / downside * Math.Sqrt(252);
    }

    public static double? MaxDrawdown(IReadOnlyList<double> prices)
    {
        if (prices.Count < 2)
        {
            return null;
        }

        double peak = prices[0];
        double maxDd = 0d;
        foreach (double value in prices)
        {
            if (value > peak)
            {
                peak = value;
            }

            if (peak <= 0d)
            {
                continue;
            }

            double dd = (peak - value) / peak;
            if (dd > maxDd)
            {
                maxDd = dd;
            }
        }

        return maxDd * 100d;
    }

    public static double? Calmar(IReadOnlyList<double> returns, double? maxDrawdownPercent)
    {
        if (returns.Count == 0 || maxDrawdownPercent is null || maxDrawdownPercent == 0d)
        {
            return null;
        }

        double annual = returns.Average() * 252d;
        return annual / (maxDrawdownPercent.Value / 100d);
    }

    public static double? VaR(IReadOnlyList<double> returns, double confidence)
    {
        if (returns.Count == 0)
        {
            return null;
        }

        double[] ordered = returns.OrderBy(static value => value).ToArray();
        int index = (int)Math.Floor((1d - confidence) * ordered.Length);
        index = Math.Clamp(index, 0, ordered.Length - 1);
        return ordered[index] * 100d;
    }

    public static double? CVaR(IReadOnlyList<double> returns, double confidence)
    {
        if (returns.Count == 0)
        {
            return null;
        }

        double? var = VaR(returns, confidence);
        if (var is null)
        {
            return null;
        }

        double threshold = var.Value / 100d;
        double[] tail = returns.Where(item => item <= threshold).ToArray();
        if (tail.Length == 0)
        {
            return null;
        }

        return tail.Average() * 100d;
    }

    public static double? ZScore(IReadOnlyList<double> prices)
    {
        if (prices.Count < 2)
        {
            return null;
        }

        double mean = prices.Average();
        double std = StdDev(prices, mean);
        if (std == 0d)
        {
            return null;
        }

        return (prices[^1] - mean) / std;
    }

    public static double? Beta(IReadOnlyList<double> assetReturns, IReadOnlyList<double> benchmarkReturns)
    {
        int length = Math.Min(assetReturns.Count, benchmarkReturns.Count);
        if (length < 2)
        {
            return null;
        }

        double[] asset = assetReturns.Take(length).ToArray();
        double[] bench = benchmarkReturns.Take(length).ToArray();
        double cov = Covariance(asset, bench);
        double variance = Variance(bench);
        if (variance == 0d)
        {
            return null;
        }

        return cov / variance;
    }

    public static double? Correlation(IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        int length = Math.Min(first.Count, second.Count);
        if (length < 2)
        {
            return null;
        }

        double[] left = first.Take(length).ToArray();
        double[] right = second.Take(length).ToArray();
        double stdLeft = Math.Sqrt(Variance(left));
        double stdRight = Math.Sqrt(Variance(right));
        if (stdLeft == 0d || stdRight == 0d)
        {
            return null;
        }

        return Covariance(left, right) / (stdLeft * stdRight);
    }

    public static IReadOnlyList<double> Returns(IReadOnlyList<decimal> prices)
    {
        if (prices.Count < 2)
        {
            return [];
        }

        List<double> result = [];
        for (int i = 1; i < prices.Count; i++)
        {
            if (prices[i - 1] == 0m)
            {
                continue;
            }

            result.Add((double)((prices[i] - prices[i - 1]) / prices[i - 1]));
        }

        return result;
    }

    private static AssetMetric Metric(string name, decimal? value, string unit, string help)
    {
        return value is null
            ? new AssetMetric(name, "Недоступно", unit, AssetRiskLevel.Unavailable, help)
            : new AssetMetric(name, $"{value:0.####}", unit, Risk(value.Value), help);
    }

    private static AssetMetric Metric(string name, double? value, string unit, string help)
    {
        return value is null
            ? new AssetMetric(name, "Недоступно", unit, AssetRiskLevel.Unavailable, help)
            : new AssetMetric(name, $"{value:0.####}", unit, Risk((decimal)value.Value), help);
    }

    private static AssetRiskLevel Risk(decimal value)
    {
        decimal abs = Math.Abs(value);
        if (abs < 1m)
        {
            return AssetRiskLevel.Low;
        }

        if (abs < 3m)
        {
            return AssetRiskLevel.Medium;
        }

        return AssetRiskLevel.High;
    }

    private static double StdDev(IEnumerable<double> values, double mean)
    {
        double[] array = values as double[] ?? values.ToArray();
        if (array.Length < 2)
        {
            return 0d;
        }

        return Math.Sqrt(array.Select(value => Math.Pow(value - mean, 2)).Average());
    }

    private static double Variance(IEnumerable<double> values)
    {
        double[] array = values as double[] ?? values.ToArray();
        if (array.Length < 2)
        {
            return 0d;
        }

        double mean = array.Average();
        return array.Select(value => Math.Pow(value - mean, 2)).Average();
    }

    private static double Covariance(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        int length = Math.Min(left.Count, right.Count);
        if (length < 2)
        {
            return 0d;
        }

        double meanLeft = left.Take(length).Average();
        double meanRight = right.Take(length).Average();
        double sum = 0d;
        for (int i = 0; i < length; i++)
        {
            sum += (left[i] - meanLeft) * (right[i] - meanRight);
        }

        return sum / length;
    }
}

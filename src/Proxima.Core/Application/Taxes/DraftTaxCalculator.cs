using System.Globalization;
using Proxima.Core.Domain.Transactions;

namespace Proxima.Core.Application.Taxes;

public sealed class DraftTaxCalculator(IExchangeRateProvider rates) : ITaxCalculator
{
    private const string Byn = "BYN";

    private static readonly string Disclaimer =
        "Расчет носит информационный характер и построен по открытым правилам МНС РБ. Он не заменяет декларацию, консультацию налогового консультанта и проверку первичных документов брокера.";

    public async Task<TaxCalculationResult> CalculateAsync(
        IReadOnlyList<TaxTransactionSnapshot> transactions,
        int reportYear,
        LegalProfileType profile,
        string baseCurrency,
        CancellationToken cancellationToken = default)
    {
        _ = baseCurrency;

        if (profile == LegalProfileType.Other)
        {
            return EmptyFailure("В профиле пользователя не выбран тип налогоплательщика.", BuildRuleSet(profile, reportYear));
        }

        List<TaxTransactionSnapshot> ordered = transactions
            .Where(t => t.TradeDate.Year <= reportYear)
            .OrderBy(t => t.TradeDate)
            .ThenBy(t => GetTransactionSortRank(t.Type))
            .ToList();

        int yearTransactionCount = ordered.Count(t => t.TradeDate.Year == reportYear);
        if (yearTransactionCount == 0)
        {
            return EmptyFailure($"За {reportYear} год нет транзакций для расчета налогов.", BuildRuleSet(profile, reportYear));
        }

        RateBook rateBook = new(rates, cancellationToken);
        decimal realizedPositive = 0m;
        decimal realizedLosses = 0m;
        decimal dividends = 0m;
        decimal rewards = 0m;
        decimal standaloneFees = 0m;
        decimal foreignTaxCreditCandidate = 0m;
        decimal currencyEffect = 0m;
        int missingCostBasisCount = 0;
        string rateSource = string.Empty;
        DateOnly? rateDate = null;

        Dictionary<Guid, Queue<TaxLot>> lotsByAsset = [];

        try
        {
            foreach (TaxTransactionSnapshot transaction in ordered)
            {
                bool belongsToReportYear = transaction.TradeDate.Year == reportYear;
                MoneyParts money = await ConvertAsync(transaction, rateBook).ConfigureAwait(false);

                if (belongsToReportYear)
                {
                    AppendRateSource(money.Source, money.Date, ref rateSource, ref rateDate);
                    decimal originalGross = GetEffectiveGrossAmount(transaction);
                    if (originalGross > 0m && !transaction.Currency.Equals(Byn, StringComparison.OrdinalIgnoreCase))
                    {
                        currencyEffect += Math.Abs(money.Gross - originalGross);
                    }
                }

                switch (transaction.Type)
                {
                    case TransactionType.Buy:
                        RegisterBuy(lotsByAsset, transaction, money);
                        break;

                    case TransactionType.Sell:
                    {
                        decimal cost = ConsumeCost(lotsByAsset, transaction, out bool costBasisMissing);
                        if (costBasisMissing && belongsToReportYear)
                        {
                            missingCostBasisCount++;
                        }

                        if (!belongsToReportYear)
                        {
                            break;
                        }

                        decimal proceeds = Math.Max(0m, money.Gross - Math.Abs(money.Fee) - Math.Abs(money.Tax));
                        decimal result = proceeds - cost;
                        if (result >= 0m)
                        {
                            realizedPositive += result;
                        }
                        else
                        {
                            realizedLosses += Math.Abs(result);
                        }

                        break;
                    }

                    case TransactionType.Dividend:
                        if (belongsToReportYear)
                        {
                            dividends += Math.Max(0m, money.Gross);
                            foreignTaxCreditCandidate += Math.Max(0m, money.Tax);
                        }

                        break;

                    case TransactionType.Airdrop:
                    case TransactionType.StakingReward:
                        if (belongsToReportYear)
                        {
                            rewards += Math.Max(0m, money.Gross);
                            foreignTaxCreditCandidate += Math.Max(0m, money.Tax);
                        }

                        break;

                    case TransactionType.Fee:
                        if (belongsToReportYear)
                        {
                            standaloneFees += Math.Abs(money.Gross) + Math.Abs(money.Fee);
                        }

                        break;

                    case TransactionType.Tax:
                        if (belongsToReportYear)
                        {
                            foreignTaxCreditCandidate += Math.Abs(money.Gross) + Math.Abs(money.Tax);
                        }

                        break;
                }
            }
        }
        catch (ExchangeRateUnavailableException ex)
        {
            return EmptyFailure(ex.Message, BuildRuleSet(profile, reportYear));
        }

        (rateSource, rateDate) = await BuildCurrentUsdRateNoteAsync(rateBook, rateSource, rateDate).ConfigureAwait(false);

        TaxRuleSet ruleSet = BuildRuleSet(profile, reportYear);
        decimal grossIncome = realizedPositive + dividends + rewards;
        decimal deductions = Math.Min(grossIncome, realizedLosses + standaloneFees);
        decimal taxableBase = Math.Max(0m, grossIncome - deductions);

        TaxComputation computed = ComputeTax(profile, taxableBase, reportYear);
        decimal foreignTaxCredit = Math.Min(computed.TotalTax, foreignTaxCreditCandidate);
        decimal totalDue = Math.Max(0m, computed.TotalTax - foreignTaxCredit);

        decimal taxSaved = decimal.Round(foreignTaxCredit + deductions, 2);
        decimal totalFees = decimal.Round(standaloneFees + foreignTaxCreditCandidate, 2);

        string message = BuildResultMessage(taxableBase, missingCostBasisCount);

        return new TaxCalculationResult(
            true,
            message,
            decimal.Round(totalDue, 2),
            decimal.Round(taxableBase, 2),
            taxSaved,
            decimal.Round(realizedPositive, 2),
            decimal.Round(dividends + rewards, 2),
            totalFees,
            decimal.Round(currencyEffect, 2),
            decimal.Round(-realizedLosses, 2),
            string.IsNullOrWhiteSpace(rateSource) ? Byn : rateSource,
            rateDate,
            ruleSet);
    }

    private static TaxCalculationResult EmptyFailure(string message, TaxRuleSet ruleSet)
    {
        return new TaxCalculationResult(false, message, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, "unavailable", null, ruleSet);
    }

    private static string BuildResultMessage(decimal taxableBase, int missingCostBasisCount)
    {
        string message = taxableBase <= 0m
            ? "По текущим операциям налоговая база не сформирована. Если за год были только покупки активов, налог к уплате обычно не возникает до продажи или получения дохода."
            : "Черновик расчета обновлен по текущему портфелю.";

        if (missingCostBasisCount > 0)
        {
            message += $" Для {missingCostBasisCount} продаж не найдена история покупки в портфеле; расходы по ним приняты как 0 BYN, проверьте импорт истории операций.";
        }

        return message;
    }

    private static async Task<(string RateSource, DateOnly? RateDate)> BuildCurrentUsdRateNoteAsync(
        RateBook rateBook,
        string currentRateSource,
        DateOnly? currentRateDate)
    {
        DateOnly displayDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        try
        {
            ExchangeRateResult usdRate = await rateBook.GetAsync("USD", Byn, displayDate).ConfigureAwait(false);
            if (usdRate.Succeeded)
            {
                return (BuildRateSourceDetail("USD", usdRate), usdRate.Date);
            }
        }
        catch
        {
            // The USD/BYN line is informational and must not invalidate the tax calculation.
        }

        string fallback = string.IsNullOrWhiteSpace(currentRateSource)
            ? "USD/BYN: курс недоступен"
            : ExtractUsdOnlyOrFallback(currentRateSource);

        DateOnly? fallbackDate = fallback.Contains("USD", StringComparison.OrdinalIgnoreCase)
            && !fallback.Contains("недоступен", StringComparison.OrdinalIgnoreCase)
                ? currentRateDate
                : null;

        return (fallback, fallbackDate);
    }

    private static void AppendRateSource(string source, DateOnly date, ref string rateSource, ref DateOnly? rateDate)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        string normalizedSource = source.Trim();
        if (!normalizedSource.Contains("USD", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(rateSource))
        {
            rateSource = normalizedSource;
            rateDate = date;
            return;
        }

        if (!rateSource.Contains("USD", StringComparison.OrdinalIgnoreCase))
        {
            rateSource = normalizedSource;
            rateDate = date;
        }
    }

    private static string ExtractUsdOnlyOrFallback(string source)
    {
        foreach (string part in source.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Contains("USD", StringComparison.OrdinalIgnoreCase))
            {
                return part;
            }
        }

        return "USD/BYN: курс недоступен";
    }

    private static TaxRuleSet BuildRuleSet(LegalProfileType profile, int reportYear)
    {
        return profile switch
        {
            LegalProfileType.SelfEmployed => new TaxRuleSet(
                $"BY-NPD-{reportYear}.01",
                new DateOnly(reportYear, 1, 1),
                10m,
                10m,
                0m,
                60_000m,
                0m,
                Disclaimer),

            LegalProfileType.IndividualEntrepreneur => new TaxRuleSet(
                $"BY-IP-{reportYear}.01",
                new DateOnly(reportYear, 1, 1),
                20m,
                20m,
                0m,
                500_000m,
                0m,
                Disclaimer),

            LegalProfileType.LLC or LegalProfileType.JSC => new TaxRuleSet(
                $"BY-PROFIT-{reportYear}.01",
                new DateOnly(reportYear, 1, 1),
                20m,
                20m,
                0m,
                0m,
                0m,
                Disclaimer),

            _ => new TaxRuleSet(
                $"BY-PIT-{reportYear}.01",
                new DateOnly(reportYear, 1, 1),
                13m,
                13m,
                0m,
                350_000m,
                600_000m,
                Disclaimer),
        };
    }

    private static TaxComputation ComputeTax(LegalProfileType profile, decimal taxableBase, int reportYear)
    {
        if (taxableBase <= 0m)
        {
            return new TaxComputation(0m);
        }

        return profile switch
        {
            LegalProfileType.SelfEmployed => new TaxComputation(ApplyTwoTier(taxableBase, 60_000m, 10m, 20m, reportYear == 2026 ? 270m : 0m)),
            LegalProfileType.IndividualEntrepreneur => new TaxComputation(ApplyTwoTier(taxableBase, 500_000m, 20m, 30m, 0m)),
            LegalProfileType.LLC or LegalProfileType.JSC => new TaxComputation(taxableBase * 20m / 100m),
            _ => new TaxComputation(ApplyThreeTier(taxableBase, 350_000m, 600_000m, 13m, 25m, 30m)),
        };
    }

    private static decimal ApplyTwoTier(decimal value, decimal threshold, decimal baseRate, decimal excessRate, decimal minimumTax)
    {
        decimal tax = value <= threshold
            ? value * baseRate / 100m
            : threshold * baseRate / 100m + (value - threshold) * excessRate / 100m;

        return Math.Max(tax, minimumTax);
    }

    private static decimal ApplyThreeTier(decimal value, decimal firstThreshold, decimal secondThreshold, decimal firstRate, decimal secondRate, decimal thirdRate)
    {
        if (value <= firstThreshold)
        {
            return value * firstRate / 100m;
        }

        if (value <= secondThreshold)
        {
            return firstThreshold * firstRate / 100m + (value - firstThreshold) * secondRate / 100m;
        }

        return firstThreshold * firstRate / 100m
            + (secondThreshold - firstThreshold) * secondRate / 100m
            + (value - secondThreshold) * thirdRate / 100m;
    }

    private static int GetTransactionSortRank(TransactionType type)
    {
        return type switch
        {
            TransactionType.Buy => 0,
            TransactionType.Split => 1,
            TransactionType.Sell => 2,
            _ => 3,
        };
    }

    private static void RegisterBuy(IDictionary<Guid, Queue<TaxLot>> lotsByAsset, TaxTransactionSnapshot transaction, MoneyParts money)
    {
        if (transaction.AssetId is null || transaction.Quantity <= 0m)
        {
            return;
        }

        Guid assetId = transaction.AssetId.Value;
        if (!lotsByAsset.TryGetValue(assetId, out Queue<TaxLot>? lots))
        {
            lots = [];
            lotsByAsset[assetId] = lots;
        }

        decimal quantity = Math.Abs(transaction.Quantity);
        decimal totalCost = Math.Max(0m, money.Gross + Math.Abs(money.Fee) + Math.Abs(money.Tax));
        if (quantity <= 0m || totalCost <= 0m)
        {
            return;
        }

        lots.Enqueue(new TaxLot(quantity, totalCost / quantity));
    }

    private static decimal ConsumeCost(IDictionary<Guid, Queue<TaxLot>> lotsByAsset, TaxTransactionSnapshot transaction, out bool costBasisMissing)
    {
        costBasisMissing = false;
        if (transaction.AssetId is null || transaction.Quantity <= 0m)
        {
            costBasisMissing = true;
            return 0m;
        }

        Guid assetId = transaction.AssetId.Value;
        if (!lotsByAsset.TryGetValue(assetId, out Queue<TaxLot>? lots) || lots.Count == 0)
        {
            costBasisMissing = true;
            return 0m;
        }

        decimal soldQuantity = Math.Abs(transaction.Quantity);
        decimal remaining = soldQuantity;
        decimal cost = 0m;

        while (remaining > 0m && lots.Count > 0)
        {
            TaxLot lot = lots.Dequeue();
            decimal used = Math.Min(remaining, lot.Quantity);
            cost += used * lot.UnitCostByn;
            remaining -= used;

            decimal left = lot.Quantity - used;
            if (left > 0m)
            {
                Queue<TaxLot> reordered = [];
                reordered.Enqueue(new TaxLot(left, lot.UnitCostByn));
                while (lots.Count > 0)
                {
                    reordered.Enqueue(lots.Dequeue());
                }

                lotsByAsset[assetId] = reordered;
                lots = reordered;
            }
        }

        if (remaining > 0m)
        {
            costBasisMissing = true;
        }

        return Math.Max(0m, cost);
    }

    private static async Task<MoneyParts> ConvertAsync(TaxTransactionSnapshot transaction, RateBook rateBook)
    {
        DateOnly date = DateOnly.FromDateTime(transaction.TradeDate.UtcDateTime.Date);
        ExchangeRateResult rate = await rateBook.GetAsync(transaction.Currency, Byn, date).ConfigureAwait(false);
        if (!rate.Succeeded)
        {
            throw new ExchangeRateUnavailableException(rate.Message);
        }

        return new MoneyParts(
            GetEffectiveGrossAmount(transaction) * rate.Rate,
            Math.Abs(transaction.FeeAmount) * rate.Rate,
            Math.Abs(transaction.TaxAmount) * rate.Rate,
            BuildRateSourceDetail(transaction.Currency, rate),
            rate.Date);
    }

    private static string BuildRateSourceDetail(string currency, ExchangeRateResult rate)
    {
        string from = NormalizeCurrency(currency);
        string provider = string.IsNullOrWhiteSpace(rate.Source) ? "unknown" : rate.Source.Trim();
        decimal effectiveRate = decimal.Round(rate.Rate, 6);

        return $"{provider}: 1 {from} = {effectiveRate.ToString("0.######", CultureInfo.InvariantCulture)} {Byn}";
    }

    private static string NormalizeCurrency(string currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? Byn
            : currency.Trim().ToUpperInvariant();
    }

    private static decimal GetEffectiveGrossAmount(TaxTransactionSnapshot transaction)
    {
        decimal gross = Math.Abs(transaction.GrossAmount);
        if (gross > 0m)
        {
            return gross;
        }

        if (transaction.Type is TransactionType.Buy or TransactionType.Sell && transaction.Quantity > 0m && transaction.Price > 0m)
        {
            return Math.Abs(transaction.Quantity * transaction.Price);
        }

        return 0m;
    }

    private sealed class RateBook(IExchangeRateProvider provider, CancellationToken cancellationToken)
    {
        private readonly Dictionary<string, ExchangeRateResult> _cache = new(StringComparer.OrdinalIgnoreCase);

        public async Task<ExchangeRateResult> GetAsync(string from, string to, DateOnly date)
        {
            string key = $"{from.Trim().ToUpperInvariant()}:{to.Trim().ToUpperInvariant()}:{date:yyyy-MM-dd}";
            if (_cache.TryGetValue(key, out ExchangeRateResult? cached))
            {
                return cached;
            }

            ExchangeRateResult result = await provider.GetRateAsync(from, to, date, cancellationToken).ConfigureAwait(false);
            _cache[key] = result;
            return result;
        }
    }

    private sealed record TaxLot(decimal Quantity, decimal UnitCostByn);

    private sealed record MoneyParts(decimal Gross, decimal Fee, decimal Tax, string Source, DateOnly Date);

    private sealed record TaxComputation(decimal TotalTax);

    private sealed class ExchangeRateUnavailableException(string message) : Exception(message);
}

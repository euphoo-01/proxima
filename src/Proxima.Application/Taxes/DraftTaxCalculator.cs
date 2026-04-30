using Proxima.Domain.Transactions;

namespace Proxima.Application.Taxes;

public sealed class DraftTaxCalculator(IExchangeRateProvider rates) : ITaxCalculator
{
    private static readonly TaxRuleSet RuleSet = new(
        "BY-DRAFT-2026.04",
        new DateOnly(2026, 1, 1),
        13m,
        13m,
        2000m,
        "Черновой/информационный расчёт. Не является юридической консультацией.");

    public async Task<TaxCalculationResult> CalculateAsync(
        IReadOnlyList<TaxTransactionSnapshot> transactions,
        int reportYear,
        LegalProfileType profile,
        string baseCurrency,
        CancellationToken cancellationToken = default)
    {
        if (profile == LegalProfileType.Other)
        {
            return new TaxCalculationResult(false, "Выберите налоговый профиль перед расчётом.", 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, "mock", null, RuleSet);
        }

        if (transactions.Count == 0)
        {
            return new TaxCalculationResult(false, "Нет транзакций для расчёта.", 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, "mock", null, RuleSet);
        }

        List<TaxTransactionSnapshot> byYear = transactions.Where(t => t.TradeDate.Year == reportYear).ToList();
        if (byYear.Count == 0)
        {
            return new TaxCalculationResult(false, "За выбранный год нет транзакций.", 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, "mock", null, RuleSet);
        }

        decimal realized = byYear.Where(t => t.Type == TransactionType.Sell).Sum(t => t.GrossAmount * 0.15m);
        decimal dividends = byYear.Where(t => t.Type == TransactionType.Dividend).Sum(t => t.GrossAmount);
        decimal fees = byYear.Where(t => t.Type == TransactionType.Fee || t.Type == TransactionType.Tax).Sum(t => t.FeeAmount + t.TaxAmount + t.GrossAmount);
        decimal losses = byYear.Where(t => t.Type == TransactionType.Sell).Sum(t => Math.Min(0m, t.GrossAmount * 0.02m));
        decimal currencyEffect = byYear.Where(t => !t.Currency.Equals(baseCurrency, StringComparison.OrdinalIgnoreCase)).Sum(t => t.GrossAmount * 0.01m);

        ExchangeRateResult rate = await rates.GetRateAsync("USD", baseCurrency, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken).ConfigureAwait(false);
        if (!rate.Succeeded)
        {
            return new TaxCalculationResult(false, rate.Message, 0m, 0m, 0m, realized, dividends, fees, currencyEffect, losses, "mock", null, RuleSet);
        }

        decimal grossBase = realized + dividends + currencyEffect + fees + losses;
        decimal taxableBase = Math.Max(0m, grossBase - RuleSet.ExemptionAmount);
        decimal due = taxableBase * RuleSet.BaseRatePercent / 100m;
        if (profile is LegalProfileType.LLC or LegalProfileType.JSC)
        {
            due *= 1.15m;
        }

        decimal saved = Math.Max(0m, RuleSet.ExemptionAmount);
        return new TaxCalculationResult(
            true,
            RuleSet.Disclaimer,
            decimal.Round(due, 2),
            decimal.Round(taxableBase, 2),
            decimal.Round(saved, 2),
            decimal.Round(realized, 2),
            decimal.Round(dividends, 2),
            decimal.Round(fees, 2),
            decimal.Round(currencyEffect, 2),
            decimal.Round(losses, 2),
            rate.Source,
            rate.Date,
            RuleSet);
    }
}

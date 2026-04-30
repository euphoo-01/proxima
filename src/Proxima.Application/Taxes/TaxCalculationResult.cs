namespace Proxima.Application.Taxes;

public sealed record TaxCalculationResult(
    bool Succeeded,
    string Message,
    decimal TaxDue,
    decimal TaxableBase,
    decimal TaxSaved,
    decimal RealizedGains,
    decimal Dividends,
    decimal Fees,
    decimal CurrencyEffect,
    decimal Losses,
    string RateSource,
    DateOnly? RateDate,
    TaxRuleSet RuleSet);

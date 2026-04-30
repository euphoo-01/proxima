namespace Proxima.Application.Taxes;

public sealed record TaxRuleSet(
    string Version,
    DateOnly EffectiveFrom,
    decimal BaseRatePercent,
    decimal DividendRatePercent,
    decimal ExemptionAmount,
    string Disclaimer);

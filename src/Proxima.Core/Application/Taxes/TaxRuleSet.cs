namespace Proxima.Core.Application.Taxes;

public sealed record TaxRuleSet(
    string Version,
    DateOnly EffectiveFrom,
    decimal BaseRatePercent,
    decimal DividendRatePercent,
    decimal ExemptionAmount,
    decimal FirstThreshold,
    decimal SecondThreshold,
    string Disclaimer);

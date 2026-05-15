namespace Proxima.Core.Application.Analytics.AssetDetails;

public sealed record AssetMetric(string Name, string Value, string Unit, AssetRiskLevel Risk, string HelpText);

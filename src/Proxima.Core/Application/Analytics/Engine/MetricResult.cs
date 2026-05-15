namespace Proxima.Core.Application.Analytics.Engine;

public sealed record MetricResult(
    string Name,
    double? Value,
    string Unit,
    MetricAvailability Availability,
    string Explanation,
    string? Severity = null);

namespace Proxima.Core.Application.Analytics.Dashboard;

public sealed record Delta24h(decimal Absolute, decimal? Percent, bool HasEnoughData);

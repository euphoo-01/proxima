namespace Proxima.Core.Application.Quotes;

public sealed record QuoteRefreshSummary(int UpdatedCount, int CachedCount, int FailedCount, string Message);

namespace Proxima.Core.Application.Analytics;

public sealed record PositionInput(
    decimal Quantity,
    decimal AverageCost,
    decimal CurrentPrice,
    decimal Fees = 0m);

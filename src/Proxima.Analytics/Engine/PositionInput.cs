namespace Proxima.Analytics.Engine;

public sealed record PositionInput(
    decimal Quantity,
    decimal AverageCost,
    decimal CurrentPrice,
    decimal Fees = 0m);

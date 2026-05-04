namespace Proxima.App.ViewModels;

public sealed record CandlestickPointViewModel(
    DateTimeOffset Timestamp,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume);

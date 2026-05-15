namespace Proxima.Core.Application.Quotes;

public sealed record QuoteData(
    string Ticker,
    decimal Price,
    string Currency,
    DateTimeOffset Timestamp,
    string Source,
    QuoteOhlc? Ohlc,
    decimal? Volume);

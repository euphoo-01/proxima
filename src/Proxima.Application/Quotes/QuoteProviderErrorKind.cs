namespace Proxima.Application.Quotes;

public enum QuoteProviderErrorKind
{
    Unknown = 0,
    Network = 1,
    RateLimited = 2,
    NotFound = 3,
    Unauthorized = 4,
}

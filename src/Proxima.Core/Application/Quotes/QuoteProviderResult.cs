namespace Proxima.Core.Application.Quotes;

public sealed record QuoteProviderResult(bool Succeeded, QuoteData? Quote, QuoteProviderErrorKind? ErrorKind, string Message)
{
    public static QuoteProviderResult Success(QuoteData quote) => new(true, quote, null, string.Empty);

    public static QuoteProviderResult Failure(QuoteProviderErrorKind kind, string message) => new(false, null, kind, message);
}

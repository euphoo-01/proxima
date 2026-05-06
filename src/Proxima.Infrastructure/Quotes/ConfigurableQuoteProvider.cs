using Proxima.Application.Auth;
using Proxima.Application.Quotes;
using Proxima.Application.Settings;

namespace Proxima.Infrastructure.Quotes;

public sealed class ConfigurableQuoteProvider(
    ICurrentUserContext currentUser,
    ISettingsService settingsService,
    HttpClient httpClient) : IQuoteProvider
{
    private readonly MockQuoteProvider _mock = new();

    public async Task<QuoteProviderResult> GetLatestQuoteAsync(
        string ticker,
        string currency,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return QuoteProviderResult.Failure(QuoteProviderErrorKind.Unknown, "Ticker is required.");
        }

        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
        {
            return await _mock.GetLatestQuoteAsync(ticker, currency, cancellationToken)
                .ConfigureAwait(false);
        }

        UserSettings? settings = await settingsService
            .GetAsync(currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (settings is null || settings.QuoteProvider == QuoteProviderKind.Mock)
        {
            return await _mock.GetLatestQuoteAsync(ticker, currency, cancellationToken)
                .ConfigureAwait(false);
        }

        if (settings.QuoteProvider == QuoteProviderKind.Finnhub)
        {
            string apiKey = UnprotectApiKey(settings.FinnhubApiKeyProtected);

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return QuoteProviderResult.Failure(
                    QuoteProviderErrorKind.Unauthorized,
                    "Finnhub API key is not configured.");
            }

            FinnhubQuoteProvider finnhub = new(httpClient, apiKey);

            return await finnhub
                .GetLatestQuoteAsync(ticker, currency, cancellationToken)
                .ConfigureAwait(false);
        }

        return await _mock.GetLatestQuoteAsync(ticker, currency, cancellationToken)
            .ConfigureAwait(false);
    }

    public static string ProtectApiKey(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(rawValue.Trim());
        return Convert.ToBase64String(bytes);
    }

    public static string UnprotectApiKey(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return string.Empty;
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(protectedValue);
            return System.Text.Encoding.UTF8.GetString(bytes).Trim();
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }
}

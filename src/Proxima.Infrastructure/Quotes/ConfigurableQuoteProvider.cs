using Proxima.Application.Auth;
using Proxima.Application.Quotes;
using Proxima.Application.Settings;

namespace Proxima.Infrastructure.Quotes;

public sealed class ConfigurableQuoteProvider(
    ICurrentUserContext currentUser,
    ISettingsService settingsService,
    HttpClient httpClient) : IQuoteProvider
{
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
            return QuoteProviderResult.Failure(
                QuoteProviderErrorKind.Unauthorized,
                "Пользователь не авторизован. Невозможно загрузить котировки Twelve Data.");
        }

        UserSettings? settings = await settingsService
            .GetAsync(currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (settings is null)
        {
            return QuoteProviderResult.Failure(
                QuoteProviderErrorKind.Unauthorized,
                "Настройки профиля не инициализированы. Сохраните Twelve Data API key в профиле.");
        }

        if (settings.QuoteProvider != QuoteProviderKind.TwelveData)
        {
            return QuoteProviderResult.Failure(
                QuoteProviderErrorKind.Unauthorized,
                "Провайдер котировок должен быть Twelve Data. Сохраните Twelve Data API key в профиле.");
        }

        string apiKey = UnprotectApiKey(settings.TwelveDataApiKeyProtected);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return QuoteProviderResult.Failure(
                QuoteProviderErrorKind.Unauthorized,
                "Twelve Data API key не задан. Откройте профиль, нажмите «API Ключи» и сохраните ключ Twelve Data.");
        }

        TwelveDataQuoteProvider provider = new(httpClient, apiKey);
        return await provider
            .GetLatestQuoteAsync(ticker, currency, cancellationToken)
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

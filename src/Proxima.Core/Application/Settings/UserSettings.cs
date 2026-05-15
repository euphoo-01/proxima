using Proxima.Core.Domain.Auth;

namespace Proxima.Core.Application.Settings;

public sealed record UserSettings(
    Guid OwnerUserId,
    string DisplayName,
    UserRole Role,
    string Login,
    string PreferredCurrency,
    AppLanguage Language,
    decimal UiScale,
    QuoteProviderKind QuoteProvider,
    int QuoteRefreshMinutes,
    string TwelveDataApiKeyProtected,
    CurrencyProviderKind CurrencyProvider);

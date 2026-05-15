using Proxima.Core.Domain.Auth;

namespace Proxima.Core.Application.Settings;

public sealed record CreateDefaultSettingsRequest(
    Guid OwnerUserId,
    string DisplayName,
    UserRole Role,
    string Login,
    string PreferredCurrency);

public sealed record UpdateSettingsRequest(
    Guid OwnerUserId,
    string DisplayName,
    UserRole Role,
    string PreferredCurrency,
    AppLanguage Language,
    decimal UiScale,
    QuoteProviderKind QuoteProvider,
    int QuoteRefreshMinutes,
    string? TwelveDataApiKeyRaw,
    CurrencyProviderKind CurrencyProvider);

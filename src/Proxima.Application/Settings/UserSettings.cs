using Proxima.Domain.Auth;

namespace Proxima.Application.Settings;

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
    string FinnhubApiKeyProtected,
    CurrencyProviderKind CurrencyProvider,
    bool SyncEnabled,
    DateTimeOffset? LastSnapshotAt);

using Proxima.Core.Domain.Auth;

namespace Proxima.Core.Application.Settings;

public sealed record UserSettings(
    Guid OwnerUserId,
    string DisplayName,
    UserRole Role,
    string Login,
    QuoteProviderKind QuoteProvider,
    string QuoteApiKey,
    CurrencyProviderKind CurrencyProvider);

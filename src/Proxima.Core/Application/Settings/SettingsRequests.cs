using Proxima.Core.Domain.Auth;

namespace Proxima.Core.Application.Settings;

public sealed record CreateDefaultSettingsRequest(
    Guid OwnerUserId,
    string DisplayName,
    UserRole Role,
    string Login);

public sealed record UpdateSettingsRequest(
    Guid OwnerUserId,
    string DisplayName,
    UserRole Role,
    QuoteProviderKind QuoteProvider,
    string? QuoteApiKeyRaw,
    CurrencyProviderKind CurrencyProvider);

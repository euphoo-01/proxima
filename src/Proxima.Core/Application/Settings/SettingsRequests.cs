namespace Proxima.Core.Application.Settings;

public sealed record CreateDefaultSettingsRequest(Guid OwnerUserId);

public sealed record UpdateSettingsRequest(
    Guid OwnerUserId,
    QuoteProviderKind QuoteProvider,
    string? QuoteApiKeyRaw,
    CurrencyProviderKind CurrencyProvider);

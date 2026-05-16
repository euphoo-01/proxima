namespace Proxima.Core.Application.Settings;

public sealed record UserSettings(
    Guid OwnerUserId,
    QuoteProviderKind QuoteProvider,
    string QuoteApiKey,
    CurrencyProviderKind CurrencyProvider);

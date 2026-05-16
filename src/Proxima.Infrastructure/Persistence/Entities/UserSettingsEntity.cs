namespace Proxima.Infrastructure.Persistence;

public sealed class UserSettingsEntity
{
    public Guid OwnerUserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string QuoteProvider { get; set; } = "TwelveData";
    public string QuoteApiKey { get; set; } = string.Empty;
    public string CurrencyProvider { get; set; } = "Mock";
}

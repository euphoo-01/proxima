namespace Proxima.Infrastructure.Persistence;

public sealed class UserSettingsEntity
{
    public Guid OwnerUserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string PreferredCurrency { get; set; } = "USD";
    public string Language { get; set; } = "RU";
    public decimal UiScale { get; set; }
    public string QuoteProvider { get; set; } = "TwelveData";
    public int QuoteRefreshMinutes { get; set; }
    public string TwelveDataApiKeyProtected { get; set; } = string.Empty;
    public string CurrencyProvider { get; set; } = "Mock";
}

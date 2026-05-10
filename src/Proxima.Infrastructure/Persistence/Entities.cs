namespace Proxima.Infrastructure.Persistence;

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string PasswordAlgorithm { get; set; } = string.Empty;
    public byte[] PasswordSalt { get; set; } = [];
    public byte[] PasswordHash { get; set; } = [];
    public int PasswordIterations { get; set; }
    public int PasswordVersion { get; set; }
    public int FailedUnlockAttempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PortfolioEntity
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "USD";
    public string? Description { get; set; }
    public string? ClientLabel { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AssetEntity
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public string? Exchange { get; set; }
    public string? Isin { get; set; }
    public string? EncryptedNotes { get; set; }
    public decimal Quantity { get; set; }
    public decimal AverageBuyPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class TagEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class AssetTagEntity
{
    public Guid AssetId { get; set; }
    public Guid TagId { get; set; }
}

public sealed class TransactionEntity
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public Guid? AssetId { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTimeOffset TradeDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Broker { get; set; }
    public string? ExternalId { get; set; }
    public string? EncryptedNotes { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AssetPriceEntity
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class GoalEntity
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal MonthlyContribution { get; set; }
    public decimal? ExpectedAnnualReturnPercent { get; set; }
    public DateTimeOffset? TargetDate { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

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
    public bool SyncEnabled { get; set; }
    public DateTimeOffset? LastSnapshotAt { get; set; }
}

public sealed class TaxProfileEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ProfileKind { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class TaxReportEntity
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public int ReportYear { get; set; }
    public decimal TaxableBase { get; set; }
    public decimal TotalTaxDue { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ImportSessionEntity
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
}

public sealed class ImportRowEntity
{
    public Guid Id { get; set; }
    public Guid ImportSessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
}

public sealed class QuoteCacheEntity
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTimeOffset Timestamp { get; set; }
    public string Source { get; set; } = string.Empty;
}

public sealed class SyncSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class NotificationEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
}

public sealed class AuditLogEntity
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string MetadataJson { get; set; } = string.Empty;
}

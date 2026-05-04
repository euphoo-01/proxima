using Microsoft.EntityFrameworkCore;

namespace Proxima.Infrastructure.Persistence;

public sealed class ProximaDbContext(DbContextOptions<ProximaDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<PortfolioEntity> Portfolios => Set<PortfolioEntity>();
    public DbSet<AssetEntity> Assets => Set<AssetEntity>();
    public DbSet<TagEntity> Tags => Set<TagEntity>();
    public DbSet<AssetTagEntity> AssetTags => Set<AssetTagEntity>();
    public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();
    public DbSet<AssetPriceEntity> AssetPrices => Set<AssetPriceEntity>();
    public DbSet<GoalEntity> Goals => Set<GoalEntity>();
    public DbSet<TaxProfileEntity> TaxProfiles => Set<TaxProfileEntity>();
    public DbSet<TaxReportEntity> TaxReports => Set<TaxReportEntity>();
    public DbSet<ImportSessionEntity> ImportSessions => Set<ImportSessionEntity>();
    public DbSet<ImportRowEntity> ImportRows => Set<ImportRowEntity>();
    public DbSet<QuoteCacheEntity> QuoteCache => Set<QuoteCacheEntity>();
    public DbSet<UserSettingsEntity> UserSettings => Set<UserSettingsEntity>();
    public DbSet<SyncSnapshotEntity> SyncSnapshots => Set<SyncSnapshotEntity>();
    public DbSet<AuditLogEntity> AuditLog => Set<AuditLogEntity>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("public");

        model.Entity<UserEntity>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.DisplayName).HasMaxLength(128);
            e.Property(x => x.Login).HasMaxLength(128);
            e.HasIndex(x => x.Login).IsUnique();
        });

        model.Entity<PortfolioEntity>(e =>
        {
            e.ToTable("portfolios");
            e.HasKey(x => x.Id);
            e.Property(x => x.BaseCurrency).HasMaxLength(8);
            e.Property(x => x.Description).HasMaxLength(1024);
            e.Property(x => x.ClientLabel).HasMaxLength(256);
            e.HasOne<UserEntity>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.OwnerUserId, x.IsArchived });
        });

        model.Entity<AssetEntity>(e =>
        {
            e.ToTable("assets");
            e.HasKey(x => x.Id);
            e.Property(x => x.Ticker).HasMaxLength(32);
            e.Property(x => x.Currency).HasMaxLength(8);
            e.Property(x => x.Exchange).HasMaxLength(128);
            e.Property(x => x.Isin).HasMaxLength(32);
            e.Property(x => x.Quantity).HasPrecision(20, 8);
            e.Property(x => x.AverageBuyPrice).HasPrecision(20, 8);
            e.Property(x => x.CurrentPrice).HasPrecision(20, 8);
            e.HasOne<PortfolioEntity>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PortfolioId, x.IsArchived });
        });

        model.Entity<TagEntity>(e =>
        {
            e.ToTable("tags");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(64);
            e.HasIndex(x => x.Name).IsUnique();
        });

        model.Entity<AssetTagEntity>(e =>
        {
            e.ToTable("asset_tags");
            e.HasKey(x => new { x.AssetId, x.TagId });
            e.HasOne<AssetEntity>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TagEntity>().WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<TransactionEntity>(e =>
        {
            e.ToTable("transactions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Quantity).HasPrecision(20, 8);
            e.Property(x => x.Price).HasPrecision(20, 8);
            e.Property(x => x.GrossAmount).HasPrecision(20, 8);
            e.Property(x => x.FeeAmount).HasPrecision(20, 8);
            e.Property(x => x.TaxAmount).HasPrecision(20, 8);
            e.Property(x => x.Currency).HasMaxLength(8);
            e.Property(x => x.Broker).HasMaxLength(128);
            e.Property(x => x.ExternalId).HasMaxLength(128);
            e.HasOne<PortfolioEntity>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AssetEntity>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PortfolioId, x.TradeDate });
        });

        model.Entity<AssetPriceEntity>(e =>
        {
            e.ToTable("asset_prices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Price).HasPrecision(20, 8);
            e.Property(x => x.Currency).HasMaxLength(8);
            e.HasOne<AssetEntity>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AssetId, x.Timestamp });
        });

        model.Entity<GoalEntity>(e =>
        {
            e.ToTable("goals");
            e.HasKey(x => x.Id);
            e.Property(x => x.TargetAmount).HasPrecision(20, 8);
            e.Property(x => x.Currency).HasMaxLength(8);
            e.Property(x => x.MonthlyContribution).HasPrecision(20, 8);
            e.HasOne<PortfolioEntity>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<TaxProfileEntity>(e =>
        {
            e.ToTable("tax_profiles");
            e.HasKey(x => x.Id);
            e.HasOne<UserEntity>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<TaxReportEntity>(e =>
        {
            e.ToTable("tax_reports");
            e.HasKey(x => x.Id);
            e.Property(x => x.TaxableBase).HasPrecision(20, 8);
            e.Property(x => x.TotalTaxDue).HasPrecision(20, 8);
            e.HasOne<PortfolioEntity>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ImportSessionEntity>(e =>
        {
            e.ToTable("import_sessions");
            e.HasKey(x => x.Id);
            e.HasOne<PortfolioEntity>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ImportRowEntity>(e =>
        {
            e.ToTable("import_rows");
            e.HasKey(x => x.Id);
            e.HasOne<ImportSessionEntity>().WithMany().HasForeignKey(x => x.ImportSessionId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<QuoteCacheEntity>(e =>
        {
            e.ToTable("quote_cache");
            e.HasKey(x => x.Id);
            e.Property(x => x.Ticker).HasMaxLength(32);
            e.Property(x => x.Price).HasPrecision(20, 8);
            e.Property(x => x.Currency).HasMaxLength(8);
            e.HasIndex(x => x.AssetId).IsUnique();
        });

        model.Entity<UserSettingsEntity>(e =>
        {
            e.ToTable("user_settings");
            e.HasKey(x => x.OwnerUserId);
            e.Property(x => x.DisplayName).HasMaxLength(128);
            e.Property(x => x.Role).HasMaxLength(64);
            e.Property(x => x.Login).HasMaxLength(128);
            e.Property(x => x.PreferredCurrency).HasMaxLength(8);
            e.Property(x => x.Language).HasMaxLength(16);
            e.Property(x => x.UiScale).HasPrecision(10, 4);
            e.Property(x => x.QuoteProvider).HasMaxLength(64);
            e.Property(x => x.FinnhubApiKeyProtected).HasMaxLength(2048);
            e.Property(x => x.CurrencyProvider).HasMaxLength(64);
            e.HasOne<UserEntity>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<SyncSnapshotEntity>(e =>
        {
            e.ToTable("sync_snapshots");
            e.HasKey(x => x.Id);
            e.HasOne<UserEntity>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<AuditLogEntity>(e =>
        {
            e.ToTable("audit_log");
            e.HasKey(x => x.Id);
            e.HasOne<UserEntity>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.Timestamp);
        });
    }
}

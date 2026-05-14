using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Proxima.Infrastructure.Persistence;

#nullable disable

namespace Proxima.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ProximaDbContext))]
public sealed class ProximaDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.HasAnnotation("ProductVersion", "9.0.4");

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(128).IsRequired();
            entity.Property(e => e.Login).HasColumnName("login").HasMaxLength(128).IsRequired();
            entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(64).IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasColumnType("text").IsRequired();
            entity.Property(e => e.FailedUnlockAttempts).HasColumnName("failed_unlock_attempts");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => e.Login).IsUnique();
        });

        modelBuilder.Entity<PortfolioEntity>(entity =>
        {
            entity.ToTable("portfolios", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.BaseCurrency).HasColumnName("base_currency").HasMaxLength(8).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1024);
            entity.Property(e => e.ClientLabel).HasColumnName("client_label").HasMaxLength(256);
            entity.Property(e => e.IsArchived).HasColumnName("is_archived");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => new { e.OwnerUserId, e.IsArchived });
            entity.HasOne<UserEntity>().WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssetEntity>(entity =>
        {
            entity.ToTable("assets", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PortfolioId).HasColumnName("portfolio_id");
            entity.Property(e => e.Ticker).HasColumnName("ticker").HasMaxLength(32).IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Type).HasColumnName("type").IsRequired();
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(8).IsRequired();
            entity.Property(e => e.Exchange).HasColumnName("exchange").HasMaxLength(128);
            entity.Property(e => e.Isin).HasColumnName("isin").HasMaxLength(32);
            entity.Property(e => e.EncryptedNotes).HasColumnName("encrypted_notes");
            entity.Property(e => e.Quantity).HasColumnName("quantity").HasPrecision(20, 8);
            entity.Property(e => e.AverageBuyPrice).HasColumnName("average_buy_price").HasPrecision(20, 8);
            entity.Property(e => e.CurrentPrice).HasColumnName("current_price").HasPrecision(20, 8);
            entity.Property(e => e.IsArchived).HasColumnName("is_archived");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => new { e.PortfolioId, e.IsArchived });
            entity.HasOne<PortfolioEntity>().WithMany().HasForeignKey(e => e.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TagEntity>(entity =>
        {
            entity.ToTable("tags", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<AssetTagEntity>(entity =>
        {
            entity.ToTable("asset_tags", "public");
            entity.HasKey(e => new { e.AssetId, e.TagId });
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.TagId).HasColumnName("tag_id");
            entity.HasOne<AssetEntity>().WithMany().HasForeignKey(e => e.AssetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<TagEntity>().WithMany().HasForeignKey(e => e.TagId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TransactionEntity>(entity =>
        {
            entity.ToTable("transactions", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PortfolioId).HasColumnName("portfolio_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.Type).HasColumnName("type").IsRequired();
            entity.Property(e => e.TradeDate).HasColumnName("trade_date");
            entity.Property(e => e.Quantity).HasColumnName("quantity").HasPrecision(20, 8);
            entity.Property(e => e.Price).HasColumnName("price").HasPrecision(20, 8);
            entity.Property(e => e.GrossAmount).HasColumnName("gross_amount").HasPrecision(20, 8);
            entity.Property(e => e.FeeAmount).HasColumnName("fee_amount").HasPrecision(20, 8);
            entity.Property(e => e.TaxAmount).HasColumnName("tax_amount").HasPrecision(20, 8);
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(8).IsRequired();
            entity.Property(e => e.Broker).HasColumnName("broker").HasMaxLength(128);
            entity.Property(e => e.ExternalId).HasColumnName("external_id").HasMaxLength(128);
            entity.Property(e => e.EncryptedNotes).HasColumnName("encrypted_notes");
            entity.Property(e => e.IsArchived).HasColumnName("is_archived");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => new { e.PortfolioId, e.TradeDate });
            entity.HasOne<PortfolioEntity>().WithMany().HasForeignKey(e => e.PortfolioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AssetEntity>().WithMany().HasForeignKey(e => e.AssetId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssetPriceEntity>(entity =>
        {
            entity.ToTable("asset_prices", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.Price).HasColumnName("price").HasPrecision(20, 8);
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(8).IsRequired();
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");
            entity.HasIndex(e => new { e.AssetId, e.Timestamp });
            entity.HasOne<AssetEntity>().WithMany().HasForeignKey(e => e.AssetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GoalEntity>(entity =>
        {
            entity.ToTable("goals", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PortfolioId).HasColumnName("portfolio_id");
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.TargetAmount).HasColumnName("target_amount").HasPrecision(20, 8);
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(8).IsRequired();
            entity.Property(e => e.MonthlyContribution).HasColumnName("monthly_contribution").HasPrecision(20, 8);
            entity.Property(e => e.ExpectedAnnualReturnPercent).HasColumnName("expected_annual_return_percent");
            entity.Property(e => e.TargetDate).HasColumnName("target_date");
            entity.Property(e => e.IsArchived).HasColumnName("is_archived");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne<PortfolioEntity>().WithMany().HasForeignKey(e => e.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QuoteCacheEntity>(entity =>
        {
            entity.ToTable("quote_cache", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.Ticker).HasColumnName("ticker").HasMaxLength(32).IsRequired();
            entity.Property(e => e.Price).HasColumnName("price").HasPrecision(20, 8);
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(8).IsRequired();
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");
            entity.Property(e => e.Source).HasColumnName("source").IsRequired();
            entity.HasIndex(e => e.AssetId).IsUnique();
        });

        modelBuilder.Entity<UserSettingsEntity>(entity =>
        {
            entity.ToTable("user_settings", "public");
            entity.HasKey(e => e.OwnerUserId);
            entity.Property(e => e.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(128).IsRequired();
            entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(64).IsRequired();
            entity.Property(e => e.Login).HasColumnName("login").HasMaxLength(128).IsRequired();
            entity.Property(e => e.PreferredCurrency).HasColumnName("preferred_currency").HasMaxLength(8).IsRequired();
            entity.Property(e => e.Language).HasColumnName("language").HasMaxLength(16).IsRequired();
            entity.Property(e => e.UiScale).HasColumnName("ui_scale").HasPrecision(10, 4);
            entity.Property(e => e.QuoteProvider).HasColumnName("quote_provider").HasMaxLength(64).IsRequired();
            entity.Property(e => e.QuoteRefreshMinutes).HasColumnName("quote_refresh_minutes");
            entity.Property(e => e.TwelveDataApiKeyProtected).HasColumnName("twelve_data_api_key_protected").HasMaxLength(2048).IsRequired();
            entity.Property(e => e.CurrencyProvider).HasColumnName("currency_provider").HasMaxLength(64).IsRequired();
            entity.HasOne<UserEntity>().WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.ToTable("notifications", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Severity).HasColumnName("severity").HasMaxLength(32).IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(120).IsRequired();
            entity.Property(e => e.Message).HasColumnName("message").HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(120).IsRequired();
            entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(e => e.DeletedAtUtc).HasColumnName("deleted_at_utc");
            entity.HasIndex(e => new { e.UserId, e.DeletedAtUtc, e.CreatedAtUtc });
            entity.HasOne<UserEntity>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLogEntity>(entity =>
        {
            entity.ToTable("audit_log", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Action).HasColumnName("action").IsRequired();
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");
            entity.Property(e => e.MetadataJson).HasColumnName("metadata_json").IsRequired();
            entity.HasIndex(e => e.Timestamp);
            entity.HasOne<UserEntity>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.SetNull);
        });
#pragma warning restore 612, 618
    }
}

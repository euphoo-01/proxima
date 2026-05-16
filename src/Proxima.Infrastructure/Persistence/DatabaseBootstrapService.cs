using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Proxima.Infrastructure.Persistence;

public sealed class DatabaseBootstrapService(DatabaseOptions options)
{
    public async Task<string> EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using ProximaDbContext context = CreateDbContext();
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            if (options.EnableSeed)
            {
                await ApplyDemoSeedAsync(context, cancellationToken).ConfigureAwait(false);
            }

            return "Database ready. EF migrations applied.";
        }
        catch (Exception ex) when (ex is NpgsqlException or IOException or UnauthorizedAccessException or System.Net.Sockets.SocketException or InvalidOperationException)
        {
            return $"Database unavailable: {ex.Message}";
        }
    }

    public ProximaDbContext CreateDbContext()
    {
        DbContextOptionsBuilder<ProximaDbContext> builder = new();
        ConfigureNpgsql(builder, options.ConnectionString);
        return new ProximaDbContext(builder.Options);
    }

    public static void ConfigureNpgsql(DbContextOptionsBuilder<ProximaDbContext> builder, string connectionString)
    {
        builder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(ProximaDbContext).Assembly.FullName));
    }

    private static async Task ApplyDemoSeedAsync(ProximaDbContext context, CancellationToken cancellationToken)
    {
        Guid userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid portfolioId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (!await context.Users.AnyAsync(x => x.Id == userId, cancellationToken).ConfigureAwait(false))
        {
            context.Users.Add(new UserEntity
            {
                Id = userId,
                DisplayName = "Demo Investor",
                Login = "demo",
                Role = "PrivateInvestor",
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        if (!await context.Portfolios.AnyAsync(x => x.Id == portfolioId, cancellationToken).ConfigureAwait(false))
        {
            context.Portfolios.Add(new PortfolioEntity
            {
                Id = portfolioId,
                OwnerUserId = userId,
                Name = "Demo Portfolio",
                Description = "Seed portfolio",
                ClientLabel = "Demo",
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        Guid appleId = Guid.Parse("33333333-3333-3333-3333-333333333331");
        Guid btcId = Guid.Parse("33333333-3333-3333-3333-333333333332");
        Guid cashId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        if (!await context.Assets.AnyAsync(x => x.Id == appleId, cancellationToken).ConfigureAwait(false))
        {
            context.Assets.AddRange(
                new AssetEntity
                {
                    Id = appleId,
                    PortfolioId = portfolioId,
                    Ticker = "AAPL",
                    Name = "Apple",
                    Type = "Stock",
                    Currency = "USD",
                    Exchange = "NASDAQ",
                    Quantity = 10m,
                    AverageBuyPrice = 150m,
                    CurrentPrice = 182m,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                new AssetEntity
                {
                    Id = btcId,
                    PortfolioId = portfolioId,
                    Ticker = "BTC",
                    Name = "Bitcoin",
                    Type = "Crypto",
                    Currency = "USD",
                    Quantity = 0.2m,
                    AverageBuyPrice = 42000m,
                    CurrentPrice = 60000m,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                new AssetEntity
                {
                    Id = cashId,
                    PortfolioId = portfolioId,
                    Ticker = "USD-CASH",
                    Name = "Cash",
                    Type = "Cash",
                    Currency = "USD",
                    Quantity = 1m,
                    AverageBuyPrice = 20000m,
                    CurrentPrice = 20000m,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
        }

        if (!await context.Transactions.AnyAsync(x => x.PortfolioId == portfolioId, cancellationToken).ConfigureAwait(false))
        {
            context.Transactions.AddRange(
                new TransactionEntity
                {
                    Id = Guid.Parse("44444444-4444-4444-4444-444444444441"),
                    PortfolioId = portfolioId,
                    AssetId = appleId,
                    Type = "Buy",
                    TradeDate = now.AddDays(-120),
                    Quantity = 10m,
                    Price = 150m,
                    GrossAmount = 1500m,
                    FeeAmount = 1m,
                    Currency = "USD",
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                new TransactionEntity
                {
                    Id = Guid.Parse("44444444-4444-4444-4444-444444444442"),
                    PortfolioId = portfolioId,
                    AssetId = btcId,
                    Type = "Buy",
                    TradeDate = now.AddDays(-90),
                    Quantity = 0.2m,
                    Price = 42000m,
                    GrossAmount = 8400m,
                    FeeAmount = 3m,
                    Currency = "USD",
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                new TransactionEntity
                {
                    Id = Guid.Parse("44444444-4444-4444-4444-444444444443"),
                    PortfolioId = portfolioId,
                    AssetId = appleId,
                    Type = "Dividend",
                    TradeDate = now.AddDays(-30),
                    Quantity = 0m,
                    Price = 0m,
                    GrossAmount = 42m,
                    FeeAmount = 0m,
                    Currency = "USD",
                    CreatedAt = now,
                    UpdatedAt = now,
                });
        }

        if (!await context.AssetPrices.AnyAsync(x => x.AssetId == appleId, cancellationToken).ConfigureAwait(false))
        {
            context.AssetPrices.AddRange(
                new AssetPriceEntity { Id = Guid.Parse("55555555-5555-5555-5555-555555555551"), AssetId = appleId, Price = 182m, Currency = "USD", Timestamp = now },
                new AssetPriceEntity { Id = Guid.Parse("55555555-5555-5555-5555-555555555552"), AssetId = btcId, Price = 60000m, Currency = "USD", Timestamp = now });
        }

        if (!await context.Goals.AnyAsync(x => x.PortfolioId == portfolioId, cancellationToken).ConfigureAwait(false))
        {
            context.Goals.Add(new GoalEntity
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666661"),
                PortfolioId = portfolioId,
                Title = "Retire",
                TargetAmount = 100000m,
                Currency = "USD",
                MonthlyContribution = 1000m,
                ExpectedAnnualReturnPercent = 8m,
                TargetDate = now.AddYears(5),
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        if (!await context.UserSettings.AnyAsync(x => x.OwnerUserId == userId, cancellationToken).ConfigureAwait(false))
        {
            context.UserSettings.Add(new UserSettingsEntity
            {
                OwnerUserId = userId,
                QuoteProvider = "TwelveData",
                QuoteApiKey = string.Empty,
                CurrencyProvider = "Mock",
            });
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

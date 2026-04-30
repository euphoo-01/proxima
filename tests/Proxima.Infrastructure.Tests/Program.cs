using Proxima.Infrastructure;
using Proxima.Application.Assets;
using Proxima.Application.Auth;
using Proxima.Application.Goals;
using Proxima.Application.Portfolios;
using Proxima.Application.Quotes;
using Proxima.Application.Settings;
using Proxima.Application.Transactions;
using Proxima.Application.Observability;
using Proxima.Domain.Assets;
using Proxima.Domain.Auth;
using Proxima.Domain.Goals;
using Proxima.Domain.Portfolios;
using Proxima.Domain.Transactions;
using Proxima.Infrastructure.Assets;
using Proxima.Infrastructure.Auth;
using Proxima.Infrastructure.Goals;
using Proxima.Infrastructure.Portfolios;
using Proxima.Infrastructure.Persistence;
using Proxima.Infrastructure.Quotes;
using Proxima.Infrastructure.Settings;
using Proxima.Infrastructure.Transactions;

namespace Proxima.Infrastructure.Tests;

internal static class Program
{
    private static async Task Main()
    {
        string? assemblyName = typeof(InfrastructureAssemblyMarker).Assembly.GetName().Name;
        Assert(assemblyName == "Proxima.Infrastructure", "Infrastructure assembly name must be Proxima.Infrastructure.");
        Pbkdf2Hasher_VerifiesPasswordAndUsesUniqueSalt();
        await JsonRepository_PersistsProfileWithoutPlaintextPassword().ConfigureAwait(false);
        await JsonPortfolioRepository_StoresAndFiltersByOwner().ConfigureAwait(false);
        await JsonAssetRepository_StoresAndArchives().ConfigureAwait(false);
        await JsonTransactionRepository_StoresAndArchives().ConfigureAwait(false);
        await JsonQuoteCacheRepository_UpsertsByAsset().ConfigureAwait(false);
        await JsonGoalRepository_StoresAndArchives().ConfigureAwait(false);
        await JsonSettingsRepository_StoresByOwner().ConfigureAwait(false);
        await DatabaseBootstrap_ReturnsGracefulMessage_WhenUnavailable().ConfigureAwait(false);
        await JsonAuditLogRepository_AppendsEvents().ConfigureAwait(false);
        RedactionHelper_RemovesSensitiveKeys();
        InitialSchemaScript_ContainsRequiredTables();
        Console.WriteLine("Proxima.Infrastructure.Tests passed.");
    }

    private static void RedactionHelper_RemovesSensitiveKeys()
    {
        string redacted = RedactionHelper.Redact("apiKey=abc token=def password=ghi");
        Assert(!redacted.Contains("apikey", StringComparison.OrdinalIgnoreCase), "Sensitive key name must be redacted.");
        Assert(!redacted.Contains("token", StringComparison.OrdinalIgnoreCase), "Sensitive token key name must be redacted.");
        Assert(!redacted.Contains("password", StringComparison.OrdinalIgnoreCase), "Sensitive password key name must be redacted.");
    }

    private static async Task JsonAuditLogRepository_AppendsEvents()
    {
        string directory = Path.Combine(Path.GetTempPath(), "proxima-audit-tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "audit.json");
        JsonAuditLogRepository repository = new(path);
        AuditService service = new(repository);
        Guid owner = Guid.Parse("11111111-1111-1111-1111-111111111111");

        await service.RecordAsync(owner, "portfolio.create", "success", "apiKey=hidden").ConfigureAwait(false);
        Assert(File.Exists(path), "Audit log must be persisted.");

        string json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        Assert(json.Contains("portfolio.create", StringComparison.Ordinal), "Audit log should contain event type.");
        Assert(!json.Contains("apiKey", StringComparison.OrdinalIgnoreCase), "Audit log should store redacted metadata.");
    }

    private static async Task DatabaseBootstrap_ReturnsGracefulMessage_WhenUnavailable()
    {
        DatabaseBootstrapService service = new(new DatabaseOptions("Host=localhost;Port=59999;Database=none;Username=none;Password=none", EnableSeed: false));
        string result = await service.EnsureReadyAsync().ConfigureAwait(false);
        Assert(result.StartsWith("Database unavailable:", StringComparison.Ordinal), "Unavailable DB should return safe guidance message.");
    }

    private static void InitialSchemaScript_ContainsRequiredTables()
    {
        string root = FindRepositoryRoot();
        string sql = File.ReadAllText(Path.Combine(root, "scripts", "sql", "0001_initial_schema.sql"));
        string[] required =
        [
            "create table if not exists users",
            "create table if not exists portfolios",
            "create table if not exists assets",
            "create table if not exists tags",
            "create table if not exists asset_tags",
            "create table if not exists transactions",
            "create table if not exists asset_prices",
            "create table if not exists goals",
            "create table if not exists tax_profiles",
            "create table if not exists tax_reports",
            "create table if not exists import_sessions",
            "create table if not exists import_rows",
            "create table if not exists quote_cache",
            "create table if not exists sync_snapshots",
            "create table if not exists audit_log",
        ];

        foreach (string table in required)
        {
            Assert(sql.Contains(table, StringComparison.OrdinalIgnoreCase), $"Schema must contain table declaration: {table}");
        }
    }

    private static async Task JsonSettingsRepository_StoresByOwner()
    {
        string directory = Path.Combine(Path.GetTempPath(), "proxima-settings-tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "settings.json");
        JsonUserSettingsRepository repository = new(path);
        SettingsService service = new(repository);
        Guid owner = Guid.Parse("99999999-1111-1111-1111-111111111111");

        await service.EnsureAsync(new CreateDefaultSettingsRequest(owner, "Demo", UserRole.PrivateInvestor, "demo", "USD")).ConfigureAwait(false);
        SettingsOperationResult updated = await service.UpdateAsync(new UpdateSettingsRequest(
            owner,
            "Demo Updated",
            UserRole.PrivateInvestor,
            "BYN",
            AppLanguage.EN,
            1.1m,
            QuoteProviderKind.Mock,
            30,
            null,
            CurrencyProviderKind.Mock,
            true)).ConfigureAwait(false);

        Assert(updated.Succeeded, "Settings update should persist.");
        UserSettings? loaded = await repository.FindByOwnerAsync(owner).ConfigureAwait(false);
        Assert(loaded is not null && loaded.PreferredCurrency == "BYN", "Settings should reload from JSON by owner.");
    }

    private static async Task JsonGoalRepository_StoresAndArchives()
    {
        string directory = Path.Combine(Path.GetTempPath(), "proxima-goal-tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "goals.json");
        GoalService service = new(new JsonGoalRepository(path));
        Guid portfolioId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        GoalOperationResult created = await service.CreateAsync(new CreateGoalRequest(portfolioId, "House", 50000m, "USD", 500m, 8m, null)).ConfigureAwait(false);
        Assert(created.Succeeded, "Goal create should persist.");

        IReadOnlyList<Goal> active = await service.ListActiveAsync(portfolioId).ConfigureAwait(false);
        Assert(active.Count == 1, "Goal should be listed.");

        GoalOperationResult archived = await service.ArchiveAsync(portfolioId, created.Goal!.Id).ConfigureAwait(false);
        Assert(archived.Succeeded, "Goal archive should persist.");
        active = await service.ListActiveAsync(portfolioId).ConfigureAwait(false);
        Assert(active.Count == 0, "Archived goal should be hidden.");
    }

    private static async Task JsonQuoteCacheRepository_UpsertsByAsset()
    {
        string directory = Path.Combine(Path.GetTempPath(), "proxima-quote-cache-tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "quotes.json");
        JsonQuoteCacheRepository repository = new(path);
        Guid assetId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");

        await repository.UpsertLatestAsync(new QuoteCacheEntry(assetId, "AAPL", 100m, "USD", DateTimeOffset.UtcNow.AddMinutes(-10), "mock"), CancellationToken.None).ConfigureAwait(false);
        await repository.UpsertLatestAsync(new QuoteCacheEntry(assetId, "AAPL", 120m, "USD", DateTimeOffset.UtcNow, "mock"), CancellationToken.None).ConfigureAwait(false);

        QuoteCacheEntry? latest = await repository.FindLatestByAssetIdAsync(assetId, CancellationToken.None).ConfigureAwait(false);
        Assert(latest is not null && latest.Price == 120m, "Latest quote must overwrite older quote for same asset.");
    }

    private static async Task JsonTransactionRepository_StoresAndArchives()
    {
        string directory = Path.Combine(Path.GetTempPath(), "proxima-transaction-tests", Guid.NewGuid().ToString("N"));
        string assetPath = Path.Combine(directory, "assets.json");
        string transactionPath = Path.Combine(directory, "transactions.json");

        JsonAssetRepository assetRepository = new(assetPath);
        AssetService assetService = new(assetRepository);
        Guid portfolioId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        AssetOperationResult asset = await assetService.CreateAsync(new CreateAssetRequest(
            portfolioId, "aapl", "Apple", AssetType.Stock, "USD", null, null, ["tech"], null, 1m, 100m, 110m)).ConfigureAwait(false);

        TransactionService service = new(new JsonTransactionRepository(transactionPath), assetRepository);
        TransactionOperationResult created = await service.CreateAsync(new CreateTransactionRequest(
            portfolioId, asset.Asset!.Id, TransactionType.Buy, DateTimeOffset.UtcNow, 1m, 110m, 110m, 0m, 0m, "USD", "Broker", null, null)).ConfigureAwait(false);
        Assert(created.Succeeded, "Transaction create should persist.");

        IReadOnlyList<PortfolioTransaction> list = await service.ListActiveAsync(portfolioId).ConfigureAwait(false);
        Assert(list.Count == 1 && list[0].Type == TransactionType.Buy, "Transaction should load from JSON storage.");

        TransactionOperationResult archived = await service.ArchiveAsync(portfolioId, created.Transaction!.Id).ConfigureAwait(false);
        Assert(archived.Succeeded, "Transaction archive should persist.");
        IReadOnlyList<PortfolioTransaction> active = await service.ListActiveAsync(portfolioId).ConfigureAwait(false);
        Assert(active.Count == 0, "Archived transaction should be hidden from active list.");
    }

    private static async Task JsonAssetRepository_StoresAndArchives()
    {
        string directory = Path.Combine(Path.GetTempPath(), "proxima-asset-tests", Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(directory, "assets.json");
        JsonAssetRepository repository = new(filePath);
        AssetService service = new(repository);
        Guid portfolioId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        AssetOperationResult created = await service.CreateAsync(new CreateAssetRequest(
            portfolioId, "btc", "Bitcoin", AssetType.Crypto, "usd", null, null, ["crypto"], null, 0.5m, 50000m, 62000m)).ConfigureAwait(false);
        Assert(created.Succeeded, "Asset create should persist.");

        IReadOnlyList<Asset> list = await service.ListActiveAsync(portfolioId).ConfigureAwait(false);
        Assert(list.Count == 1 && list[0].Ticker == "BTC", "Asset should load from JSON storage.");

        AssetOperationResult archived = await service.ArchiveAsync(portfolioId, created.Asset!.Id).ConfigureAwait(false);
        Assert(archived.Succeeded, "Asset archive should persist.");
        IReadOnlyList<Asset> active = await service.ListActiveAsync(portfolioId).ConfigureAwait(false);
        Assert(active.Count == 0, "Archived asset should be hidden from active list.");
    }

    private static void Pbkdf2Hasher_VerifiesPasswordAndUsesUniqueSalt()
    {
        Pbkdf2PasswordHasher hasher = new();

        PasswordCredential first = hasher.Hash("Proxima2026");
        PasswordCredential second = hasher.Hash("Proxima2026");

        Assert(first.Hash.Length > 0, "Password hash must not be empty.");
        Assert(first.Salt.Length > 0, "Password salt must not be empty.");
        Assert(!first.Salt.SequenceEqual(second.Salt), "Same password must use different salts.");
        Assert(!first.Hash.SequenceEqual(second.Hash), "Same password with different salts must produce different hashes.");
        Assert(hasher.Verify("Proxima2026", first), "Correct password must verify.");
        Assert(!hasher.Verify("WrongPassword2026", first), "Incorrect password must fail.");
    }

    private static async Task JsonRepository_PersistsProfileWithoutPlaintextPassword()
    {
        string directory = Path.Combine(Path.GetTempPath(), "proxima-auth-tests", Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(directory, "profiles.json");
        JsonLocalUserRepository repository = new(filePath);
        LocalAuthService service = new(repository, new Pbkdf2PasswordHasher(), new PasswordPolicyValidator());

        AuthResult created = await service.CreateProfileAsync(new CreateProfileRequest(
            "Demo Investor",
            "demo",
            "Proxima2026!",
            "Proxima2026!",
            UserRole.PrivateInvestor)).ConfigureAwait(false);

        Assert(created.Succeeded, "Profile setup must persist through repository.");
        Assert(File.Exists(filePath), "Profile store file must exist.");

        string persistedJson = File.ReadAllText(filePath);
        Assert(!persistedJson.Contains("Proxima2026", StringComparison.Ordinal), "Profile store must not contain plaintext password.");
        Assert(!persistedJson.Contains("Proxima2026!", StringComparison.Ordinal), "Profile store must not contain plaintext password with symbols.");
        Assert(persistedJson.Contains(Pbkdf2PasswordHasher.AlgorithmName, StringComparison.Ordinal), "Profile store must record password algorithm metadata.");

        JsonLocalUserRepository reloadedRepository = new(filePath);
        LocalUserProfile? reloaded = await reloadedRepository.FindByLoginAsync("demo", CancellationToken.None).ConfigureAwait(false);
        Assert(reloaded is not null, "Persisted profile must reload by login.");

        AuthResult unlocked = await new LocalAuthService(reloadedRepository, new Pbkdf2PasswordHasher(), new PasswordPolicyValidator())
            .UnlockAsync("demo", "Proxima2026!")
            .ConfigureAwait(false);
        Assert(unlocked.Succeeded, "Reloaded profile must unlock with correct password.");
    }

    private static async Task JsonPortfolioRepository_StoresAndFiltersByOwner()
    {
        string directory = Path.Combine(Path.GetTempPath(), "proxima-portfolio-tests", Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(directory, "portfolios.json");
        JsonPortfolioRepository repository = new(filePath);
        PortfolioService service = new(repository);

        Guid firstOwner = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid secondOwner = Guid.Parse("22222222-2222-2222-2222-222222222222");

        PortfolioOperationResult first = await service.CreateAsync(new CreatePortfolioRequest(firstOwner, "Owner1", "USD", null, null)).ConfigureAwait(false);
        PortfolioOperationResult second = await service.CreateAsync(new CreatePortfolioRequest(secondOwner, "Owner2", "EUR", null, null)).ConfigureAwait(false);
        Assert(first.Succeeded && second.Succeeded, "Portfolio create should persist for multiple owners.");

        IReadOnlyList<Portfolio> firstList = await service.ListActiveAsync(firstOwner).ConfigureAwait(false);
        IReadOnlyList<Portfolio> secondList = await service.ListActiveAsync(secondOwner).ConfigureAwait(false);
        Assert(firstList.Count == 1 && firstList[0].Name == "Owner1", "Owner1 should only see own portfolios.");
        Assert(secondList.Count == 1 && secondList[0].Name == "Owner2", "Owner2 should only see own portfolios.");

        await service.ArchiveAsync(firstOwner, first.Portfolio!.Id).ConfigureAwait(false);
        IReadOnlyList<Portfolio> firstAfterArchive = await service.ListActiveAsync(firstOwner).ConfigureAwait(false);
        Assert(firstAfterArchive.Count == 0, "Archived portfolio should be hidden from active list.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Proxima.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test runner output directory.");
    }
}

using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Proxima.Application.Assets;
using Proxima.Application.Auth;
using Proxima.Application.Goals;
using Proxima.Application.Observability;
using Proxima.Application.Portfolios;
using Proxima.Application.Quotes;
using Proxima.Application.Settings;
using Proxima.Application.Taxes;
using Proxima.Application.Transactions;
using Proxima.Domain.Assets;
using Proxima.Domain.Auth;
using Proxima.Domain.Goals;
using Proxima.Domain.Portfolios;
using Proxima.Domain.Transactions;
using Proxima.Infrastructure;
using Proxima.Infrastructure.Auth;
using Proxima.Infrastructure.Persistence;
using Proxima.Infrastructure.Persistence.Repositories;
using Proxima.Infrastructure.Quotes;
using Proxima.Infrastructure.Taxes;

namespace Proxima.Infrastructure.Tests;

internal static class Program
{
    private static async Task Main()
    {
        string? assemblyName = typeof(InfrastructureAssemblyMarker).Assembly.GetName().Name;
        Assert(assemblyName == "Proxima.Infrastructure", "Infrastructure assembly name must be Proxima.Infrastructure.");

        Pbkdf2Hasher_VerifiesPasswordAndUsesUniqueSalt();
        await PostgresLocalUserRepository_StoresProfileWithoutPlaintextPassword().ConfigureAwait(false);
        await PostgresPortfolioRepository_StoresAndFiltersByOwner().ConfigureAwait(false);
        await PostgresAssetRepository_StoresTagsAndArchives().ConfigureAwait(false);
        await PostgresTransactionRepository_StoresAndArchives().ConfigureAwait(false);
        await PostgresQuoteCacheRepository_UpsertsByAsset().ConfigureAwait(false);
        await PostgresGoalRepository_StoresAndArchives().ConfigureAwait(false);
        await PostgresSettingsRepository_StoresByOwner().ConfigureAwait(false);
        await PostgresAuditLogRepository_AppendsRedactedEvents().ConfigureAwait(false);
        await TwelveDataQuoteProvider_ParsesQuotePayload().ConfigureAwait(false);
        await BelarusbankExchangeRateProvider_ParsesRatesPayload().ConfigureAwait(false);
        await DatabaseBootstrap_ReturnsGracefulMessage_WhenUnavailable().ConfigureAwait(false);
        Repositories_UseUnitOfWorkPattern();
        NoJsonPersistenceRepositories_ArePresent();
        RedactionHelper_RemovesSensitiveKeys();
        EfMigrations_ReplaceSqlScriptsAndCleanLegacyTables();

        Console.WriteLine("Proxima.Infrastructure.Tests passed.");
    }

    private static async Task PostgresLocalUserRepository_StoresProfileWithoutPlaintextPassword()
    {
        TestRepositoryFixture fixture = TestRepositoryFixture.Create();
        LocalAuthService service = new(
            new PostgresLocalUserRepository(fixture.UnitOfWorkFactory, fixture.UnitOfWorkAccessor),
            new Pbkdf2PasswordHasher(),
            new PasswordPolicyValidator());

        AuthResult created = await service.CreateProfileAsync(new CreateProfileRequest(
            "Demo Investor",
            "demo",
            "Proxima2026!",
            "Proxima2026!",
            UserRole.PrivateInvestor)).ConfigureAwait(false);

        Assert(created.Succeeded, "Profile setup must persist through database repository.");
        AuthResult unlocked = await service.UnlockAsync("demo", "Proxima2026!").ConfigureAwait(false);
        Assert(unlocked.Succeeded, "Persisted profile must unlock with correct password.");

        await using ProximaDbContext verify = fixture.CreateContext();
        UserEntity row = await verify.Users.AsNoTracking().SingleAsync().ConfigureAwait(false);
        string combinedCredentialText = $"{row.PasswordAlgorithm}:{Convert.ToBase64String(row.PasswordSalt)}:{Convert.ToBase64String(row.PasswordHash)}";
        Assert(!combinedCredentialText.Contains("Proxima2026", StringComparison.Ordinal), "Database credentials must not contain plaintext password.");
        Assert(row.PasswordAlgorithm == Pbkdf2PasswordHasher.AlgorithmName, "Database credentials must store password algorithm metadata.");
        Assert(row.FailedUnlockAttempts == 0, "Successful unlock must reset failed attempt counter.");
    }

    private static async Task PostgresPortfolioRepository_StoresAndFiltersByOwner()
    {
        TestRepositoryFixture fixture = TestRepositoryFixture.Create();
        PortfolioService service = new(new PostgresPortfolioRepository(fixture.UnitOfWorkFactory, fixture.UnitOfWorkAccessor));

        Guid firstOwner = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid secondOwner = Guid.Parse("22222222-2222-2222-2222-222222222222");
        await fixture.SeedUserAsync(firstOwner, "owner1").ConfigureAwait(false);
        await fixture.SeedUserAsync(secondOwner, "owner2").ConfigureAwait(false);

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

    private static async Task PostgresAssetRepository_StoresTagsAndArchives()
    {
        TestRepositoryFixture fixture = TestRepositoryFixture.Create();
        Guid ownerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        Guid portfolioId = await fixture.SeedPortfolioAsync(ownerId).ConfigureAwait(false);

        AssetService service = new(new PostgresAssetRepository(fixture.UnitOfWorkFactory, fixture.UnitOfWorkAccessor));
        AssetOperationResult created = await service.CreateAsync(new CreateAssetRequest(
            portfolioId, "btc", "Bitcoin", AssetType.Crypto, "usd", null, null, ["crypto", "long"], null, 0.5m, 50000m, 62000m)).ConfigureAwait(false);
        Assert(created.Succeeded, "Asset create should persist.");

        IReadOnlyList<Asset> list = await service.ListActiveAsync(portfolioId).ConfigureAwait(false);
        Assert(list.Count == 1 && list[0].Ticker == "BTC", "Asset should load from database storage.");
        Assert(list[0].Tags.Count == 2 && list[0].Tags.Contains("crypto"), "Asset tags should persist in normalized tables.");

        AssetOperationResult archived = await service.ArchiveAsync(portfolioId, created.Asset!.Id).ConfigureAwait(false);
        Assert(archived.Succeeded, "Asset archive should persist.");
        IReadOnlyList<Asset> active = await service.ListActiveAsync(portfolioId).ConfigureAwait(false);
        Assert(active.Count == 0, "Archived asset should be hidden from active list.");
    }

    private static async Task PostgresTransactionRepository_StoresAndArchives()
    {
        TestRepositoryFixture fixture = TestRepositoryFixture.Create();
        Guid ownerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        Guid portfolioId = await fixture.SeedPortfolioAsync(ownerId).ConfigureAwait(false);

        PostgresAssetRepository assetRepository = new(fixture.UnitOfWorkFactory, fixture.UnitOfWorkAccessor);
        AssetService assetService = new(assetRepository);
        AssetOperationResult asset = await assetService.CreateAsync(new CreateAssetRequest(
            portfolioId, "aapl", "Apple", AssetType.Stock, "USD", null, null, ["tech"], null, 1m, 100m, 110m)).ConfigureAwait(false);

        TransactionService service = new(new PostgresTransactionRepository(fixture.UnitOfWorkFactory, fixture.UnitOfWorkAccessor), assetRepository);
        TransactionOperationResult created = await service.CreateAsync(new CreateTransactionRequest(
            portfolioId, asset.Asset!.Id, TransactionType.Buy, DateTimeOffset.UtcNow, 1m, 110m, 110m, 0m, 0m, "USD", "Broker", null, null)).ConfigureAwait(false);
        Assert(created.Succeeded, "Transaction create should persist.");

        IReadOnlyList<PortfolioTransaction> list = await service.ListActiveAsync(portfolioId).ConfigureAwait(false);
        Assert(list.Count == 1 && list[0].Type == TransactionType.Buy, "Transaction should load from database storage.");

        TransactionOperationResult archived = await service.ArchiveAsync(portfolioId, created.Transaction!.Id).ConfigureAwait(false);
        Assert(archived.Succeeded, "Transaction archive should persist.");
        IReadOnlyList<PortfolioTransaction> active = await service.ListActiveAsync(portfolioId).ConfigureAwait(false);
        Assert(active.Count == 0, "Archived transaction should be hidden from active list.");
    }

    private static async Task PostgresQuoteCacheRepository_UpsertsByAsset()
    {
        TestRepositoryFixture fixture = TestRepositoryFixture.Create();
        Guid assetId = await fixture.SeedAssetAsync().ConfigureAwait(false);
        PostgresQuoteCacheRepository repository = new(fixture.UnitOfWorkFactory, fixture.UnitOfWorkAccessor);

        await repository.UpsertLatestAsync(new QuoteCacheEntry(assetId, "AAPL", 100m, "USD", DateTimeOffset.UtcNow.AddMinutes(-10), "mock"), CancellationToken.None).ConfigureAwait(false);
        await repository.UpsertLatestAsync(new QuoteCacheEntry(assetId, "AAPL", 120m, "USD", DateTimeOffset.UtcNow, "mock"), CancellationToken.None).ConfigureAwait(false);

        QuoteCacheEntry? latest = await repository.FindLatestByAssetIdAsync(assetId, CancellationToken.None).ConfigureAwait(false);
        Assert(latest is not null && latest.Price == 120m, "Latest quote must overwrite older quote for same asset.");
    }

    private static async Task PostgresGoalRepository_StoresAndArchives()
    {
        TestRepositoryFixture fixture = TestRepositoryFixture.Create();
        Guid ownerId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        Guid portfolioId = await fixture.SeedPortfolioAsync(ownerId).ConfigureAwait(false);
        GoalService service = new(new PostgresGoalRepository(fixture.UnitOfWorkFactory, fixture.UnitOfWorkAccessor));

        GoalOperationResult created = await service.CreateAsync(new CreateGoalRequest(portfolioId, "House", 50000m, "USD", 500m, 8m, null)).ConfigureAwait(false);
        Assert(created.Succeeded, "Goal create should persist.");

        IReadOnlyList<Goal> active = await service.ListActiveAsync(portfolioId).ConfigureAwait(false);
        Assert(active.Count == 1, "Goal should be listed.");

        GoalOperationResult archived = await service.ArchiveAsync(portfolioId, created.Goal!.Id).ConfigureAwait(false);
        Assert(archived.Succeeded, "Goal archive should persist.");
        active = await service.ListActiveAsync(portfolioId).ConfigureAwait(false);
        Assert(active.Count == 0, "Archived goal should be hidden.");
    }

    private static async Task PostgresSettingsRepository_StoresByOwner()
    {
        TestRepositoryFixture fixture = TestRepositoryFixture.Create();
        PostgresUserSettingsRepository repository = new(fixture.UnitOfWorkFactory, fixture.UnitOfWorkAccessor);
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
            CurrencyProviderKind.Mock)).ConfigureAwait(false);

        Assert(updated.Succeeded, "Settings update should persist.");
        UserSettings? loaded = await repository.FindByOwnerAsync(owner).ConfigureAwait(false);
        Assert(loaded is not null && loaded.PreferredCurrency == "BYN", "Settings should reload from database by owner.");
    }

    private static async Task PostgresAuditLogRepository_AppendsRedactedEvents()
    {
        TestRepositoryFixture fixture = TestRepositoryFixture.Create();
        PostgresAuditLogRepository repository = new(fixture.UnitOfWorkFactory, fixture.UnitOfWorkAccessor);
        AuditService service = new(repository);
        Guid owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
        await fixture.SeedUserAsync(owner, "audit-owner").ConfigureAwait(false);

        await service.RecordAsync(owner, "portfolio.create", "success", "apiKey=hidden").ConfigureAwait(false);

        await using ProximaDbContext verify = fixture.CreateContext();
        AuditLogEntity row = await verify.AuditLog.AsNoTracking().SingleAsync().ConfigureAwait(false);
        Assert(row.Action == "portfolio.create", "Audit log should contain event type.");
        Assert(!row.MetadataJson.Contains("apiKey", StringComparison.OrdinalIgnoreCase), "Audit log should store redacted metadata.");
    }

    private static async Task TwelveDataQuoteProvider_ParsesQuotePayload()
    {
        const string payload = """{"symbol":"AAPL","currency":"USD","open":"121.0","high":"126.0","low":"120.5","close":"123.45","volume":"1000","timestamp":1714500000}""";
        using HttpClient client = new(new StubHttpMessageHandler(payload))
        {
            BaseAddress = new Uri("https://api.twelvedata.com"),
        };

        TwelveDataQuoteProvider provider = new(client, "demo-key");
        QuoteProviderResult result = await provider.GetLatestQuoteAsync("AAPL", "USD").ConfigureAwait(false);

        Assert(result.Succeeded, "Twelve Data provider should parse valid payload.");
        Assert(result.Quote is not null, "Quote should be present.");
        Assert(result.Quote!.Price == 123.45m, "Quote price should match response.");
        Assert(result.Quote.Ohlc is not null && result.Quote.Ohlc.Close == 123.45m, "OHLC close should map from current price.");
        Assert(result.Quote.Source == "twelvedata", "Quote source should be twelvedata.");
    }

    private static async Task BelarusbankExchangeRateProvider_ParsesRatesPayload()
    {
        const string payload = """[{"USD_out":"3.2","EUR_out":"3.5","RUB_out":"0.034"}]""";
        using HttpClient client = new(new StubHttpMessageHandler(payload))
        {
            BaseAddress = new Uri("https://belarusbank.by"),
        };

        BelarusbankExchangeRateProvider provider = new(client);
        ExchangeRateResult result = await provider.GetRateAsync("USD", "BYN", DateOnly.FromDateTime(DateTime.UtcNow)).ConfigureAwait(false);

        Assert(result.Succeeded, "Belarusbank provider should parse valid payload.");
        Assert(result.Rate == 3.2m, "USD->BYN rate should be parsed from payload.");
        Assert(result.Source == "belarusbank", "Rate source should be belarusbank.");
    }

    private static async Task DatabaseBootstrap_ReturnsGracefulMessage_WhenUnavailable()
    {
        DatabaseBootstrapService service = new(new DatabaseOptions("Host=localhost;Port=59999;Database=none;Username=none;Password=none", EnableSeed: false));
        string result = await service.EnsureReadyAsync().ConfigureAwait(false);
        Assert(result.StartsWith("Database unavailable:", StringComparison.Ordinal), "Unavailable DB should return safe guidance message.");
    }

    private static void Repositories_UseUnitOfWorkPattern()
    {
        string root = FindRepositoryRoot();
        string repoDir = Path.Combine(root, "src", "Proxima.Infrastructure", "Persistence", "Repositories");
        string[] repos = Directory.GetFiles(repoDir, "Postgres*.cs", SearchOption.TopDirectoryOnly);
        Assert(repos.Length >= 9, "Expected database repositories should be present.");

        foreach (string file in repos)
        {
            string code = File.ReadAllText(file);
            if (Path.GetFileName(file) == "PostgresImportCommitService.cs")
            {
                Assert(code.Contains("IProximaUnitOfWorkFactory", StringComparison.Ordinal), "Import commit service should use UnitOfWork factory.");
                continue;
            }

            Assert(code.Contains("IProximaUnitOfWorkFactory", StringComparison.Ordinal), $"Repository should depend on UnitOfWork factory: {Path.GetFileName(file)}");
            Assert(code.Contains("UowLease", StringComparison.Ordinal), $"Repository should resolve context through UowLease: {Path.GetFileName(file)}");
        }
    }

    private static void NoJsonPersistenceRepositories_ArePresent()
    {
        string root = FindRepositoryRoot();
        string infra = Path.Combine(root, "src", "Proxima.Infrastructure");
        string[] legacyStoreMarkers =
        [
            "profile" + "-store" + ".json",
            "asset" + "-store" + ".json",
            "portfolio" + "-store" + ".json",
            "transaction" + "-store" + ".json",
            "settings" + ".json",
        ];

        string[] forbidden = Directory
            .EnumerateFiles(infra, "*.cs", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).StartsWith("Json", StringComparison.OrdinalIgnoreCase)
                || legacyStoreMarkers.Any(marker => File.ReadAllText(path).Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert(forbidden.Length == 0, "Infrastructure must not contain JSON persistence repositories or JSON store paths.");
    }

    private static void RedactionHelper_RemovesSensitiveKeys()
    {
        string redacted = RedactionHelper.Redact("apiKey=abc token=def password=ghi");
        Assert(!redacted.Contains("apikey", StringComparison.OrdinalIgnoreCase), "Sensitive key name must be redacted.");
        Assert(!redacted.Contains("token", StringComparison.OrdinalIgnoreCase), "Sensitive token key name must be redacted.");
        Assert(!redacted.Contains("password", StringComparison.OrdinalIgnoreCase), "Sensitive password key name must be redacted.");
    }

    private static void EfMigrations_ReplaceSqlScriptsAndCleanLegacyTables()
    {
        string root = FindRepositoryRoot();
        string sqlDir = Path.Combine(root, "scripts", "sql");
        Assert(!Directory.Exists(sqlDir), "Legacy scripts/sql directory must be removed after EF migrations adoption.");

        string migrationDir = Path.Combine(root, "src", "Proxima.Infrastructure", "Persistence", "Migrations");
        string[] migrations = Directory.GetFiles(migrationDir, "*.cs", SearchOption.TopDirectoryOnly);
        Assert(migrations.Any(path => Path.GetFileName(path).Contains("InitialPostgreSqlSchema", StringComparison.Ordinal)), "Initial EF migration must be present.");
        Assert(migrations.Any(path => Path.GetFileName(path).Contains("RemoveSyncAndUnusedTables", StringComparison.Ordinal)), "Cleanup EF migration must be present.");

        string model = File.ReadAllText(Path.Combine(root, "src", "Proxima.Infrastructure", "Persistence", "ProximaDbContext.cs"));
        Assert(!model.Contains("SyncSnapshotEntity", StringComparison.Ordinal), "DbContext must not map sync snapshots after sync module removal.");
        Assert(!model.Contains("TaxReportEntity", StringComparison.Ordinal), "DbContext must not map unused tax report table.");
        Assert(!model.Contains("ImportSessionEntity", StringComparison.Ordinal), "DbContext must not map unused import session table.");
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

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class StubHttpMessageHandler(string payload, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        private readonly string _payload = payload;
        private readonly HttpStatusCode _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(_statusCode)
            {
                Content = new StringContent(_payload, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class TestRepositoryFixture
    {
        private readonly DbContextOptions<ProximaDbContext> _options;

        private TestRepositoryFixture(DbContextOptions<ProximaDbContext> options)
        {
            _options = options;
            UnitOfWorkAccessor = new ProximaUnitOfWorkAccessor();
            UnitOfWorkFactory = new TestUnitOfWorkFactory(options, UnitOfWorkAccessor);
        }

        public IProximaUnitOfWorkAccessor UnitOfWorkAccessor { get; }

        public IProximaUnitOfWorkFactory UnitOfWorkFactory { get; }

        public static TestRepositoryFixture Create()
        {
            DbContextOptions<ProximaDbContext> options = new DbContextOptionsBuilder<ProximaDbContext>()
                .UseInMemoryDatabase($"proxima-tests-{Guid.NewGuid():N}")
                .Options;

            TestRepositoryFixture fixture = new(options);
            using ProximaDbContext context = fixture.CreateContext();
            context.Database.EnsureCreated();
            return fixture;
        }

        public ProximaDbContext CreateContext()
        {
            return new ProximaDbContext(_options);
        }

        public async Task SeedUserAsync(Guid userId, string login)
        {
            await using ProximaDbContext context = CreateContext();
            if (await context.Users.AnyAsync(x => x.Id == userId).ConfigureAwait(false))
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            context.Users.Add(new UserEntity
            {
                Id = userId,
                DisplayName = login,
                Login = login,
                Role = UserRole.PrivateInvestor.ToString(),
                PasswordAlgorithm = string.Empty,
                PasswordSalt = [],
                PasswordHash = [],
                PasswordIterations = 0,
                PasswordVersion = 0,
                FailedUnlockAttempts = 0,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<Guid> SeedPortfolioAsync(Guid ownerId)
        {
            string generatedLogin = $"user-{ownerId:N}";
            await SeedUserAsync(ownerId, generatedLogin[..Math.Min(20, generatedLogin.Length)]).ConfigureAwait(false);
            await using ProximaDbContext context = CreateContext();
            Guid portfolioId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            context.Portfolios.Add(new PortfolioEntity
            {
                Id = portfolioId,
                OwnerUserId = ownerId,
                Name = "Default",
                BaseCurrency = "USD",
                CreatedAt = now,
                UpdatedAt = now,
            });
            await context.SaveChangesAsync().ConfigureAwait(false);
            return portfolioId;
        }

        public async Task<Guid> SeedAssetAsync()
        {
            Guid ownerId = Guid.NewGuid();
            Guid portfolioId = await SeedPortfolioAsync(ownerId).ConfigureAwait(false);
            await using ProximaDbContext context = CreateContext();
            Guid assetId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            context.Assets.Add(new AssetEntity
            {
                Id = assetId,
                PortfolioId = portfolioId,
                Ticker = "AAPL",
                Name = "Apple",
                Type = AssetType.Stock.ToString(),
                Currency = "USD",
                Quantity = 1m,
                AverageBuyPrice = 100m,
                CurrentPrice = 120m,
                IsArchived = false,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await context.SaveChangesAsync().ConfigureAwait(false);
            return assetId;
        }
    }

    private sealed class TestUnitOfWorkFactory(
        DbContextOptions<ProximaDbContext> options,
        IProximaUnitOfWorkAccessor accessor) : IProximaUnitOfWorkFactory
    {
        public IProximaUnitOfWork Create()
        {
            return new TestUnitOfWork(new ProximaDbContext(options));
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<IProximaUnitOfWork, Task<T>> action, CancellationToken cancellationToken = default)
        {
            await using IProximaUnitOfWork uow = Create();
            IProximaUnitOfWork? previous = accessor.Current;
            accessor.Current = uow;
            try
            {
                T result = await action(uow).ConfigureAwait(false);
                await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return result;
            }
            finally
            {
                accessor.Current = previous;
            }
        }

        public async Task ExecuteInTransactionAsync(Func<IProximaUnitOfWork, Task> action, CancellationToken cancellationToken = default)
        {
            await ExecuteInTransactionAsync(async uow =>
            {
                await action(uow).ConfigureAwait(false);
                return 0;
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class TestUnitOfWork(ProximaDbContext context) : IProximaUnitOfWork
    {
        public ProximaDbContext Context { get; } = context;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Context.SaveChangesAsync(cancellationToken);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("The in-memory test unit of work does not support database transactions.");
        }

        public void Dispose()
        {
            Context.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            return Context.DisposeAsync();
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

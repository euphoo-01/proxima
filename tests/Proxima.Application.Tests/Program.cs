using Proxima.Application;
using Proxima.Application.Assets;
using Proxima.Application.Auth;
using Proxima.Application.Goals;
using Proxima.Application.Portfolios;
using Proxima.Application.Quotes;
using Proxima.Application.Taxes;
using Proxima.Application.Transactions;
using Proxima.Domain.Assets;
using Proxima.Domain.Auth;
using Proxima.Domain.Goals;
using Proxima.Domain.Portfolios;
using Proxima.Domain.Transactions;

namespace Proxima.Application.Tests;

internal static class Program
{
    private static async Task Main()
    {
        string? assemblyName = typeof(ApplicationAssemblyMarker).Assembly.GetName().Name;
        Assert(assemblyName == "Proxima.Application", "Application assembly name must be Proxima.Application.");
        PasswordValidator_RejectsWeakPasswords();
        PasswordValidator_AcceptsStrongPassword();
        await AuthService_CreatesProfileAndUnlocks().ConfigureAwait(false);
        await AuthService_ReturnsGenericErrorForUnknownLoginAndWrongPassword().ConfigureAwait(false);
        PortfolioService_CreateUpdateArchiveFlow().GetAwaiter().GetResult();
        PortfolioService_IsolatesByOwner().GetAwaiter().GetResult();
        AssetService_NormalizesTickerAndArchives().GetAwaiter().GetResult();
        TransactionService_ValidatesCrossPortfolioAsset().GetAwaiter().GetResult();
        QuoteRefreshService_UsesCacheOnProviderFailure().GetAwaiter().GetResult();
        GoalService_ForecastAndValidation().GetAwaiter().GetResult();
        TaxCalculator_IsDeterministicAndIncludesDraftMetadata().GetAwaiter().GetResult();
        TaxCalculator_ReturnsRecoverableError_WhenRateUnavailable().GetAwaiter().GetResult();
        Console.WriteLine("Proxima.Application.Tests passed.");
    }

    private static async Task TaxCalculator_IsDeterministicAndIncludesDraftMetadata()
    {
        DraftTaxCalculator calculator = new(new FixedRateProvider(3.2m));
        IReadOnlyList<TaxTransactionSnapshot> tx =
        [
            new TaxTransactionSnapshot(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), TransactionType.Sell, 1000m, 0m, 0m, "USD"),
            new TaxTransactionSnapshot(new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero), TransactionType.Dividend, 150m, 0m, 0m, "USD"),
        ];

        TaxCalculationResult first = await calculator.CalculateAsync(tx, 2026, LegalProfileType.PhysicalPerson, "USD").ConfigureAwait(false);
        TaxCalculationResult second = await calculator.CalculateAsync(tx, 2026, LegalProfileType.PhysicalPerson, "USD").ConfigureAwait(false);

        Assert(first.Succeeded, "Tax calculation should succeed for valid input.");
        Assert(first.TaxDue == second.TaxDue, "Tax calculation should be deterministic for equal input.");
        Assert(first.RuleSet.Version.StartsWith("BY-DRAFT-", StringComparison.Ordinal), "Tax rule set must carry version metadata.");
        Assert(first.Message.Contains("Черновой", StringComparison.OrdinalIgnoreCase), "Draft disclaimer must be included.");
    }

    private static async Task TaxCalculator_ReturnsRecoverableError_WhenRateUnavailable()
    {
        DraftTaxCalculator calculator = new(new FailingRateProvider());
        IReadOnlyList<TaxTransactionSnapshot> tx =
        [
            new TaxTransactionSnapshot(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), TransactionType.Sell, 500m, 0m, 0m, "USD"),
        ];

        TaxCalculationResult result = await calculator.CalculateAsync(tx, 2026, LegalProfileType.PhysicalPerson, "BYN").ConfigureAwait(false);
        Assert(!result.Succeeded, "Tax calculation should fail recoverably when exchange rate provider fails.");
        Assert(result.Message.Contains("Курс", StringComparison.OrdinalIgnoreCase), "User-visible rate failure message expected.");
    }

    private static async Task GoalService_ForecastAndValidation()
    {
        MemoryGoalRepository repository = new();
        GoalService service = new(repository);
        Guid portfolioId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        GoalOperationResult invalid = await service.CreateAsync(new CreateGoalRequest(portfolioId, "", 1000m, "USD", 100m, 8m, null)).ConfigureAwait(false);
        Assert(!invalid.Succeeded, "Goal title is required.");

        GoalOperationResult created = await service.CreateAsync(new CreateGoalRequest(portfolioId, "Retire", 10000m, "USD", 300m, 8m, null)).ConfigureAwait(false);
        Assert(created.Succeeded, "Valid goal should be created.");

        GoalForecast forecast = service.Forecast(created.Goal!, currentPortfolioValue: 2000m);
        Assert(forecast.Reachable, "Goal should be reachable under positive contribution.");
    }

    private static async Task QuoteRefreshService_UsesCacheOnProviderFailure()
    {
        MemoryAssetRepository assets = new();
        MemoryQuoteCacheRepository cache = new();
        Guid portfolioId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Asset asset = new(Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"), portfolioId, "FAIL-X", "Fail Asset", AssetType.Stock, "USD", null, null, [], null, 1m, 0m, 0m, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await assets.AddAsync(asset, CancellationToken.None).ConfigureAwait(false);
        await cache.UpsertLatestAsync(new QuoteCacheEntry(asset.Id, asset.Ticker, 123.45m, "USD", DateTimeOffset.UtcNow, "cache"), CancellationToken.None).ConfigureAwait(false);

        QuoteRefreshService service = new(assets, new FailingQuoteProvider(), cache);
        QuoteRefreshSummary summary = await service.RefreshPortfolioAsync(portfolioId).ConfigureAwait(false);

        Assert(summary.CachedCount == 1, "Cached quote should be used when provider fails.");
        Asset? updated = await assets.FindByIdAsync(portfolioId, asset.Id, CancellationToken.None).ConfigureAwait(false);
        Assert(updated is not null && updated.CurrentPrice == 123.45m, "Cached price should patch asset.");
    }

    private static async Task TransactionService_ValidatesCrossPortfolioAsset()
    {
        MemoryAssetRepository assets = new();
        MemoryTransactionRepository transactions = new();
        TransactionService service = new(transactions, assets);
        Guid portfolioA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid portfolioB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        Asset assetB = new(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), portfolioB, "MSFT", "Microsoft", AssetType.Stock, "USD", null, null, [], null, 1m, 100m, 110m, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await assets.AddAsync(assetB, CancellationToken.None).ConfigureAwait(false);

        TransactionOperationResult rejected = await service.CreateAsync(new CreateTransactionRequest(
            portfolioA, assetB.Id, TransactionType.Buy, DateTimeOffset.UtcNow, 1m, 100m, 100m, 0m, 0m, "USD", null, null, null)).ConfigureAwait(false);
        Assert(!rejected.Succeeded, "Cross-portfolio asset reference must be rejected.");

        Asset assetA = new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), portfolioA, "AAPL", "Apple", AssetType.Stock, "USD", null, null, [], null, 1m, 100m, 110m, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await assets.AddAsync(assetA, CancellationToken.None).ConfigureAwait(false);
        TransactionOperationResult accepted = await service.CreateAsync(new CreateTransactionRequest(
            portfolioA, assetA.Id, TransactionType.Buy, DateTimeOffset.UtcNow, 2m, 123.45m, 246.90m, 1m, 0m, "USD", "BRK", null, null)).ConfigureAwait(false);
        Assert(accepted.Succeeded, "Same-portfolio transaction should persist.");
    }

    private static async Task AssetService_NormalizesTickerAndArchives()
    {
        MemoryAssetRepository repository = new();
        AssetService service = new(repository);
        Guid portfolioId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        AssetOperationResult created = await service.CreateAsync(new CreateAssetRequest(
            portfolioId, " aapl ", "Apple", AssetType.Stock, "usd", null, null, ["tech", "growth"], "note", 3m, 100m, 120m)).ConfigureAwait(false);
        Assert(created.Succeeded, "Asset create should succeed.");
        Assert(created.Asset!.Ticker == "AAPL", "Ticker should be normalized to uppercase.");

        AssetOperationResult archived = await service.ArchiveAsync(portfolioId, created.Asset.Id).ConfigureAwait(false);
        Assert(archived.Succeeded, "Asset archive should succeed.");

        IReadOnlyList<Asset> active = await service.ListActiveAsync(portfolioId).ConfigureAwait(false);
        Assert(active.Count == 0, "Archived asset should not appear in active list.");
    }

    private static async Task PortfolioService_CreateUpdateArchiveFlow()
    {
        MemoryPortfolioRepository repository = new();
        PortfolioService service = new(repository);
        Guid owner = Guid.Parse("11111111-1111-1111-1111-111111111111");

        PortfolioOperationResult created = await service.CreateAsync(new CreatePortfolioRequest(owner, "Main", "USD", null, null)).ConfigureAwait(false);
        Assert(created.Succeeded, "Portfolio create should succeed.");

        PortfolioOperationResult updated = await service.UpdateAsync(new UpdatePortfolioRequest(owner, created.Portfolio!.Id, "Main 2", "EUR", "Desc", "Client")).ConfigureAwait(false);
        Assert(updated.Succeeded, "Portfolio update should succeed.");
        Assert(updated.Portfolio!.BaseCurrency == "EUR", "Portfolio currency should update.");

        PortfolioOperationResult archived = await service.ArchiveAsync(owner, created.Portfolio.Id).ConfigureAwait(false);
        Assert(archived.Succeeded, "Portfolio archive should succeed.");

        IReadOnlyList<Portfolio> active = await service.ListActiveAsync(owner).ConfigureAwait(false);
        Assert(active.Count == 0, "Archived portfolio should be hidden from active list.");
    }

    private static async Task PortfolioService_IsolatesByOwner()
    {
        MemoryPortfolioRepository repository = new();
        PortfolioService service = new(repository);
        Guid first = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid second = Guid.Parse("22222222-2222-2222-2222-222222222222");

        await service.CreateAsync(new CreatePortfolioRequest(first, "Owner A", "USD", null, null)).ConfigureAwait(false);
        await service.CreateAsync(new CreatePortfolioRequest(second, "Owner B", "USD", null, null)).ConfigureAwait(false);

        IReadOnlyList<Portfolio> firstList = await service.ListActiveAsync(first).ConfigureAwait(false);
        IReadOnlyList<Portfolio> secondList = await service.ListActiveAsync(second).ConfigureAwait(false);

        Assert(firstList.Count == 1 && firstList[0].Name == "Owner A", "Owner A should only see own portfolio.");
        Assert(secondList.Count == 1 && secondList[0].Name == "Owner B", "Owner B should only see own portfolio.");
    }

    private static void PasswordValidator_RejectsWeakPasswords()
    {
        PasswordPolicyValidator validator = new();

        Assert(!validator.Validate("short1", "short1").IsValid, "Short password must be rejected.");
        Assert(!validator.Validate("password", "password").IsValid, "Password without digit or symbol must be rejected.");
        Assert(!validator.Validate("password1", "different1").IsValid, "Mismatched confirmation must be rejected.");
    }

    private static void PasswordValidator_AcceptsStrongPassword()
    {
        PasswordValidationResult result = new PasswordPolicyValidator().Validate("Proxima2026", "Proxima2026");
        Assert(result.IsValid, "Strong matching password must be accepted.");
    }

    private static async Task AuthService_CreatesProfileAndUnlocks()
    {
        MemoryUserRepository repository = new();
        LocalAuthService service = new(repository, new TestPasswordHasher(), new PasswordPolicyValidator());

        Assert(await service.NeedsFirstRunSetupAsync().ConfigureAwait(false), "Empty repository must require first-run setup.");

        AuthResult created = await service.CreateProfileAsync(new CreateProfileRequest(
            "Investor",
            "Demo",
            "Proxima2026",
            "Proxima2026",
            UserRole.PrivateInvestor)).ConfigureAwait(false);

        Assert(created.Succeeded, "Valid profile setup must succeed.");
        Assert(!await service.NeedsFirstRunSetupAsync().ConfigureAwait(false), "Created profile must disable first-run setup.");

        AuthResult unlocked = await service.UnlockAsync("demo", "Proxima2026").ConfigureAwait(false);
        Assert(unlocked.Succeeded, "Correct password must unlock.");
        Assert(repository.UpdatedProfiles.Count > 0, "Successful unlock must update profile state.");
    }

    private static async Task AuthService_ReturnsGenericErrorForUnknownLoginAndWrongPassword()
    {
        MemoryUserRepository repository = new();
        LocalAuthService service = new(repository, new TestPasswordHasher(), new PasswordPolicyValidator());

        await service.CreateProfileAsync(new CreateProfileRequest(
            "Investor",
            "demo",
            "Proxima2026",
            "Proxima2026",
            UserRole.PrivateInvestor)).ConfigureAwait(false);

        AuthResult unknownLogin = await service.UnlockAsync("missing", "Proxima2026").ConfigureAwait(false);
        AuthResult wrongPassword = await service.UnlockAsync("demo", "wrong-password").ConfigureAwait(false);

        Assert(!unknownLogin.Succeeded, "Unknown login must fail.");
        Assert(!wrongPassword.Succeeded, "Wrong password must fail.");
        Assert(
            unknownLogin.FailureReason == AuthFailureReason.GenericAuthenticationFailed
            && wrongPassword.FailureReason == AuthFailureReason.GenericAuthenticationFailed,
            "Unknown login and wrong password must use the same failure reason.");
        Assert(unknownLogin.UserMessage == wrongPassword.UserMessage, "Unknown login and wrong password must show the same message.");
        Assert(repository.Profiles.Single().FailedUnlockAttempts == 1, "Wrong password must increment failed attempts.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class MemoryUserRepository : ILocalUserRepository
    {
        private readonly List<LocalUserProfile> _profiles = [];

        public IReadOnlyList<LocalUserProfile> Profiles => _profiles;

        public List<LocalUserProfile> UpdatedProfiles { get; } = [];

        public Task<bool> HasAnyProfileAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_profiles.Count > 0);
        }

        public Task<LocalUserProfile?> FindByLoginAsync(string login, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_profiles.FirstOrDefault(profile => profile.Login == login));
        }

        public Task AddAsync(LocalUserProfile profile, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _profiles.Add(profile);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(LocalUserProfile profile, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = _profiles.FindIndex(existing => existing.Id == profile.Id);
            _profiles[index] = profile;
            UpdatedProfiles.Add(profile);
            return Task.CompletedTask;
        }
    }

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public PasswordCredential Hash(string password)
        {
            return new PasswordCredential("test", [1, 2, 3], System.Text.Encoding.UTF8.GetBytes("hashed:" + password), 1, 1);
        }

        public bool Verify(string password, PasswordCredential credential)
        {
            return credential.Hash.SequenceEqual(System.Text.Encoding.UTF8.GetBytes("hashed:" + password));
        }
    }

    private sealed class FixedRateProvider(decimal rate) : IExchangeRateProvider
    {
        public Task<ExchangeRateResult> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ExchangeRateResult.Success(rate, "test", date));
        }
    }

    private sealed class FailingRateProvider : IExchangeRateProvider
    {
        public Task<ExchangeRateResult> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ExchangeRateResult.Failure("Курс недоступен, попробуйте позже."));
        }
    }

    private sealed class MemoryPortfolioRepository : IPortfolioRepository
    {
        private readonly List<Portfolio> _portfolios = [];

        public Task<IReadOnlyList<Portfolio>> ListByOwnerAsync(Guid ownerUserId, bool includeArchived, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerable<Portfolio> query = _portfolios.Where(item => item.OwnerUserId == ownerUserId);
            if (!includeArchived)
            {
                query = query.Where(item => !item.IsArchived);
            }

            return Task.FromResult<IReadOnlyList<Portfolio>>(query.ToArray());
        }

        public Task<Portfolio?> FindByIdAsync(Guid ownerUserId, Guid portfolioId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_portfolios.FirstOrDefault(item => item.OwnerUserId == ownerUserId && item.Id == portfolioId));
        }

        public Task AddAsync(Portfolio portfolio, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _portfolios.Add(portfolio);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Portfolio portfolio, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = _portfolios.FindIndex(item => item.Id == portfolio.Id && item.OwnerUserId == portfolio.OwnerUserId);
            _portfolios[index] = portfolio;
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryAssetRepository : IAssetRepository
    {
        private readonly List<Asset> _items = [];

        public Task<IReadOnlyList<Asset>> ListByPortfolioAsync(Guid portfolioId, bool includeArchived, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerable<Asset> query = _items.Where(item => item.PortfolioId == portfolioId);
            if (!includeArchived)
            {
                query = query.Where(item => !item.IsArchived);
            }

            return Task.FromResult<IReadOnlyList<Asset>>(query.ToArray());
        }

        public Task<Asset?> FindByIdAsync(Guid portfolioId, Guid assetId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_items.FirstOrDefault(item => item.PortfolioId == portfolioId && item.Id == assetId));
        }

        public Task AddAsync(Asset asset, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _items.Add(asset);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Asset asset, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = _items.FindIndex(item => item.PortfolioId == asset.PortfolioId && item.Id == asset.Id);
            _items[index] = asset;
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryTransactionRepository : ITransactionRepository
    {
        private readonly List<PortfolioTransaction> _items = [];

        public Task<IReadOnlyList<PortfolioTransaction>> ListByPortfolioAsync(Guid portfolioId, bool includeArchived, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerable<PortfolioTransaction> query = _items.Where(item => item.PortfolioId == portfolioId);
            if (!includeArchived)
            {
                query = query.Where(item => !item.IsArchived);
            }

            return Task.FromResult<IReadOnlyList<PortfolioTransaction>>(query.ToArray());
        }

        public Task<PortfolioTransaction?> FindByIdAsync(Guid portfolioId, Guid transactionId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_items.FirstOrDefault(item => item.PortfolioId == portfolioId && item.Id == transactionId));
        }

        public Task AddAsync(PortfolioTransaction transaction, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _items.Add(transaction);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PortfolioTransaction transaction, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = _items.FindIndex(item => item.PortfolioId == transaction.PortfolioId && item.Id == transaction.Id);
            _items[index] = transaction;
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryQuoteCacheRepository : IQuoteCacheRepository
    {
        private readonly Dictionary<Guid, QuoteCacheEntry> _items = [];

        public Task<QuoteCacheEntry?> FindLatestByAssetIdAsync(Guid assetId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _items.TryGetValue(assetId, out QuoteCacheEntry? value);
            return Task.FromResult(value);
        }

        public Task UpsertLatestAsync(QuoteCacheEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _items[entry.AssetId] = entry;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingQuoteProvider : IQuoteProvider
    {
        public Task<QuoteProviderResult> GetLatestQuoteAsync(string ticker, string currency, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(QuoteProviderResult.Failure(QuoteProviderErrorKind.Network, "offline"));
        }
    }

    private sealed class MemoryGoalRepository : IGoalRepository
    {
        private readonly List<Goal> _items = [];

        public Task<IReadOnlyList<Goal>> ListByPortfolioAsync(Guid portfolioId, bool includeArchived, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerable<Goal> query = _items.Where(item => item.PortfolioId == portfolioId);
            if (!includeArchived)
            {
                query = query.Where(item => !item.IsArchived);
            }

            return Task.FromResult<IReadOnlyList<Goal>>(query.ToArray());
        }

        public Task<Goal?> FindByIdAsync(Guid portfolioId, Guid goalId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_items.FirstOrDefault(item => item.PortfolioId == portfolioId && item.Id == goalId));
        }

        public Task AddAsync(Goal goal, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _items.Add(goal);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Goal goal, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int idx = _items.FindIndex(item => item.PortfolioId == goal.PortfolioId && item.Id == goal.Id);
            _items[idx] = goal;
            return Task.CompletedTask;
        }
    }
}

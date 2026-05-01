using Proxima.Analytics;
using Proxima.App.Controls;
using Proxima.App.ViewModels;
using Proxima.Application;
using Proxima.Application.Assets;
using Proxima.Application.Auth;
using Proxima.Application.Goals;
using Proxima.Application.Portfolios;
using Proxima.Application.Quotes;
using Proxima.Application.Settings;
using Proxima.Application.Taxes;
using Proxima.Application.Transactions;
using Proxima.Application.Observability;
using Proxima.Domain.Assets;
using Proxima.Domain;
using Proxima.Domain.Auth;
using Proxima.Domain.Goals;
using Proxima.Domain.Portfolios;
using Proxima.Domain.Transactions;
using Proxima.Importing;
using Proxima.Sync.Snapshots;
using Proxima.Infrastructure;
using Proxima.Reporting;
using Proxima.Reporting.Reports;
using Proxima.Sync;

namespace Proxima.App.Tests;

internal static class Program
{
    private static void Main()
    {
        AssemblyNames_AreStable();
        Domain_HasNoForbiddenDependencies();
        DesignSystem_FilesExist();
        DesignSystem_ControlsExposeBindingProperties();
        FigmaInspection_DocumentsCustomMcpSource();
        AuthUi_UsesMaskedPasswordInputsAndRecoveryCopy();
        AuthViewModel_InitializesSetupAndUnlockStates();
        NavigationService_RegistersRoutesAndSupportsBack();
        ShellViewModel_UpdatesActivePageAndBreadcrumb();
        ShellViewModel_PreservesSelectedPortfolioAcrossNavigation().GetAwaiter().GetResult();
        ShellViewModel_FiltersAndSortsAssets().GetAwaiter().GetResult();
        ShellViewModel_LoadsAndFiltersTransactions().GetAwaiter().GetResult();
        ShellViewModel_ComputesDashboardCards().GetAwaiter().GetResult();
        ShellViewModel_BuildsAssetDetailsMetrics().GetAwaiter().GetResult();
        ShellViewModel_HandlesQuoteRefreshFailureGracefully().GetAwaiter().GetResult();
        ShellViewModel_HandlesTaxRecalcFailureGracefully().GetAwaiter().GetResult();
        SettingsPage_ContainsMaskedApiKeyInput();
        SnapshotService_EncryptDecryptAndTamperFail().GetAwaiter().GetResult();
        ReportingService_ExportsNonEmptyPdf().GetAwaiter().GetResult();
        Localization_FallbackAndLanguageSwitch_Works().GetAwaiter().GetResult();
        NotificationHost_IsBoundInShellLayout();
        RedactionHelper_RedactsSensitiveFragments();
        Charts_ArePresentInShellLayout();
        Console.WriteLine("Proxima.App.Tests baseline checks passed.");
    }

    private static void Charts_ArePresentInShellLayout()
    {
        string repositoryRoot = FindRepositoryRoot();
        string xamlPath = Path.Combine(repositoryRoot, "src", "Proxima.App", "MainWindow.axaml");
        string xaml = File.ReadAllText(xamlPath);
        Assert(xaml.Contains("controls:LineChart", StringComparison.Ordinal), "Shell should include line chart controls.");
        Assert(xaml.Contains("controls:CandlestickChart", StringComparison.Ordinal), "Asset Details should include candlestick chart control.");
    }

    private static void RedactionHelper_RedactsSensitiveFragments()
    {
        string redacted = RedactionHelper.Redact("token=abc password=hello apiKey=xyz");
        Assert(!redacted.Contains("password", StringComparison.OrdinalIgnoreCase), "Redaction should remove password key fragments.");
        Assert(!redacted.Contains("token", StringComparison.OrdinalIgnoreCase), "Redaction should remove token key fragments.");
    }

    private static void NotificationHost_IsBoundInShellLayout()
    {
        string repositoryRoot = FindRepositoryRoot();
        string xamlPath = Path.Combine(repositoryRoot, "src", "Proxima.App", "MainWindow.axaml");
        string xaml = File.ReadAllText(xamlPath);
        Assert(xaml.Contains("ItemsSource=\"{Binding Shell.Notifications}\"", StringComparison.Ordinal), "Shell must render notification items.");
    }

    private static async Task Localization_FallbackAndLanguageSwitch_Works()
    {
        ShellViewModel shell = CreateShellViewModel();
        await shell.InitializeAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Test User", "test", UserRole.PrivateInvestor).ConfigureAwait(false);
        shell.SettingsLanguage = AppLanguage.EN;
        Assert(shell.NavDashboardText == "Dashboard", "Navigation label should switch to EN.");
        Assert(shell.PageTitle is "Dashboard" or "Assets" or "Taxes" or "Goals" or "Settings", "Page title should use localized labels.");
    }

    private static async Task SnapshotService_EncryptDecryptAndTamperFail()
    {
        string root = Path.Combine(Path.GetTempPath(), "proxima-sync-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "portfolios.json"), "[{\"id\":\"demo\"}]").ConfigureAwait(false);
        LocalEncryptedSnapshotService service = new(root, "1.0.0", "device-a");

        SnapshotExportResult exported = await service.ExportAsync("Password123!").ConfigureAwait(false);
        Assert(exported.Succeeded && !string.IsNullOrWhiteSpace(exported.FilePath), "Snapshot export should succeed.");

        SnapshotPreviewResult preview = await service.PreviewImportAsync(exported.FilePath!, "Password123!").ConfigureAwait(false);
        Assert(preview.Succeeded, "Snapshot preview should decrypt with valid password.");

        string json = await File.ReadAllTextAsync(exported.FilePath!).ConfigureAwait(false);
        string tampered = json.Replace("A", "B", StringComparison.Ordinal);
        string tamperedPath = Path.Combine(root, "tampered.pxsnap");
        await File.WriteAllTextAsync(tamperedPath, tampered).ConfigureAwait(false);
        SnapshotPreviewResult bad = await service.PreviewImportAsync(tamperedPath, "Password123!").ConfigureAwait(false);
        Assert(!bad.Succeeded, "Tampered snapshot should fail safely.");
    }

    private static async Task ReportingService_ExportsNonEmptyPdf()
    {
        string outDir = Path.Combine(Path.GetTempPath(), "proxima-report-tests", Guid.NewGuid().ToString("N"));
        SimplePdfReportService service = new();
        PortfolioReportRequest request = new(
            "Main",
            "1M",
            1000m,
            55m,
            [("Tech", 600m)],
            [("AAPL", 500m)],
            [("Sharpe", "1.2")],
            5,
            "USD",
            "Draft",
            outDir);

        ReportExportResult result = await service.ExportPortfolioPdfAsync(request).ConfigureAwait(false);
        Assert(result.Succeeded && result.OutputPath is not null, "Portfolio PDF export should succeed.");
        FileInfo file = new(result.OutputPath!);
        Assert(file.Exists && file.Length > 100, "Generated PDF should exist and be non-empty.");
    }

    private static void SettingsPage_ContainsMaskedApiKeyInput()
    {
        string repositoryRoot = FindRepositoryRoot();
        string xamlPath = Path.Combine(repositoryRoot, "src", "Proxima.App", "MainWindow.axaml");
        string xaml = File.ReadAllText(xamlPath);
        Assert(xaml.Contains("Finnhub API key", StringComparison.Ordinal), "Settings page must include Finnhub API key input.");
        Assert(xaml.Contains("PasswordChar=\"•\"", StringComparison.Ordinal), "API key field must be masked.");
    }

    private static void AssemblyNames_AreStable()
    {
        AssertAssemblyName(typeof(DomainAssemblyMarker), "Proxima.Domain");
        AssertAssemblyName(typeof(ApplicationAssemblyMarker), "Proxima.Application");
        AssertAssemblyName(typeof(InfrastructureAssemblyMarker), "Proxima.Infrastructure");
        AssertAssemblyName(typeof(AnalyticsAssemblyMarker), "Proxima.Analytics");
        AssertAssemblyName(typeof(ImportingAssemblyMarker), "Proxima.Importing");
        AssertAssemblyName(typeof(ReportingAssemblyMarker), "Proxima.Reporting");
        AssertAssemblyName(typeof(SyncAssemblyMarker), "Proxima.Sync");
    }

    private static void Domain_HasNoForbiddenDependencies()
    {
        string[] forbidden =
        [
            "Avalonia",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "System.Net.Http",
        ];

        string[] references = typeof(DomainAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        foreach (string forbiddenReference in forbidden)
        {
            bool hasForbiddenReference = references.Any(reference =>
                reference.StartsWith(forbiddenReference, StringComparison.Ordinal));

            Assert(!hasForbiddenReference, $"Domain must not reference {forbiddenReference}.");
        }
    }

    private static void AssertAssemblyName(Type markerType, string expectedName)
    {
        string? assemblyName = markerType.Assembly.GetName().Name;
        Assert(assemblyName == expectedName, $"Assembly name must be {expectedName}.");
    }

    private static void DesignSystem_FilesExist()
    {
        string root = FindRepositoryRoot();
        string[] requiredFiles =
        [
            "docs/figma-inspection.md",
            "src/Proxima.App/Styles/Tokens.axaml",
            "src/Proxima.App/Styles/Typography.axaml",
            "src/Proxima.App/Styles/Buttons.axaml",
            "src/Proxima.App/Styles/Inputs.axaml",
            "src/Proxima.App/Styles/Cards.axaml",
            "src/Proxima.App/Styles/Tables.axaml",
            "src/Proxima.App/Styles/Charts.axaml",
            "src/Proxima.App/Controls/BentoCard.cs",
            "src/Proxima.App/Controls/MetricCard.cs",
            "src/Proxima.App/Controls/StatusPill.cs",
            "src/Proxima.App/Controls/EmptyState.cs",
            "src/Proxima.App/Controls/PageHeader.cs",
            "src/Proxima.App/Controls/SearchBox.cs",
            "src/Proxima.App/Controls/TimeframeSelector.cs",
            "src/Proxima.App/Controls/DataTableHeaderCell.cs",
        ];

        foreach (string file in requiredFiles)
        {
            Assert(File.Exists(Path.Combine(root, file)), $"Required design-system file is missing: {file}");
        }

        string tokens = File.ReadAllText(Path.Combine(root, "src/Proxima.App/Styles/Tokens.axaml"));
        foreach (string token in new[] { "ProximaBrush.Page", "ProximaBrush.Surface", "ProximaBrush.TextPrimary", "ProximaBrush.Success", "ProximaBrush.Warning", "ProximaBrush.Danger", "ProximaBrush.Focus", "ProximaRadius.Card", "ProximaShadow.Card" })
        {
            Assert(tokens.Contains(token, StringComparison.Ordinal), $"Tokens.axaml must define {token}.");
        }

        string appStyles = File.ReadAllText(Path.Combine(root, "src/Proxima.App/App.axaml"));
        foreach (string dictionary in new[] { "Tokens.axaml", "Typography.axaml", "Buttons.axaml", "Inputs.axaml", "Cards.axaml", "Tables.axaml", "Charts.axaml" })
        {
            Assert(appStyles.Contains(dictionary, StringComparison.Ordinal), $"App.axaml must include {dictionary}.");
        }
    }

    private static void DesignSystem_ControlsExposeBindingProperties()
    {
        Assert(BentoCard.TitleProperty.Name == nameof(BentoCard.Title), "BentoCard must expose TitleProperty.");
        Assert(BentoCard.SubtitleProperty.Name == nameof(BentoCard.Subtitle), "BentoCard must expose SubtitleProperty.");
        Assert(BentoCard.CommandProperty.Name == nameof(BentoCard.Command), "BentoCard must expose CommandProperty.");
        Assert(MetricCard.ValueProperty.Name == nameof(MetricCard.Value), "MetricCard must expose ValueProperty.");
        Assert(MetricCard.DeltaKindProperty.Name == nameof(MetricCard.DeltaKind), "MetricCard must expose DeltaKindProperty.");
        Assert(StatusPill.TextProperty.Name == nameof(StatusPill.Text), "StatusPill must expose TextProperty.");
        Assert(EmptyState.MessageProperty.Name == nameof(EmptyState.Message), "EmptyState must expose MessageProperty.");
        Assert(PageHeader.TitleProperty.Name == nameof(PageHeader.Title), "PageHeader must expose TitleProperty.");
        Assert(DataTableHeaderCell.TextProperty.Name == nameof(DataTableHeaderCell.Text), "DataTableHeaderCell must expose TextProperty.");
    }

    private static void FigmaInspection_DocumentsCustomMcpSource()
    {
        string inspection = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs/figma-inspection.md"));
        string[] requiredPhrases =
        [
            "Drxcen3JN69XP0fnYxkgOi",
            "62:497",
            "62:498",
            "62:2751",
            "mcp__figma__",
            "Extracted Tokens",
            "Screenshots",
            "Differences from Figma",
            "Structured node data captured",
        ];

        foreach (string phrase in requiredPhrases)
        {
            Assert(inspection.Contains(phrase, StringComparison.Ordinal), $"Figma inspection must document {phrase}.");
        }
    }

    private static void AuthUi_UsesMaskedPasswordInputsAndRecoveryCopy()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src/Proxima.App/MainWindow.axaml"));
        Assert(xaml.Contains("PasswordChar=\"•\"", StringComparison.Ordinal), "Auth UI must use masked password inputs.");
        Assert(xaml.Contains("PasswordChar=\"•\"", StringComparison.Ordinal), "Password inputs must be masked.");
        Assert(xaml.Contains("ForgotPasswordClicked", StringComparison.Ordinal), "Auth UI must expose forgot-password recovery action.");
        Assert(xaml.Contains("IsVisible=\"{Binding IsUnlocked}\"", StringComparison.Ordinal), "Shell must be gated by unlock state.");
        Assert(xaml.Contains("Tag=\"settings\"", StringComparison.Ordinal), "Sidebar must contain Settings route.");
        Assert(xaml.Contains("SelectedItem=\"{Binding Shell.SelectedPortfolio}\"", StringComparison.Ordinal), "Topbar must bind portfolio selector.");
        Assert(xaml.Contains("OpenCreatePortfolioClicked", StringComparison.Ordinal), "Topbar create-portfolio button must open dialog.");
    }

    private static void AuthViewModel_InitializesSetupAndUnlockStates()
    {
        AuthViewModel setupViewModel = new(new TestAuthService(needsSetup: true), CreateShellViewModel());
        setupViewModel.InitializeAsync().GetAwaiter().GetResult();
        Assert(setupViewModel.IsSetupMode, "Empty auth store must show setup mode.");
        Assert(!setupViewModel.IsUnlocked, "Setup mode must not start unlocked.");

        AuthViewModel unlockViewModel = new(new TestAuthService(needsSetup: false), CreateShellViewModel());
        unlockViewModel.InitializeAsync().GetAwaiter().GetResult();
        Assert(unlockViewModel.IsLoginMode, "Existing profile must show unlock mode.");
    }

    private static void NavigationService_RegistersRoutesAndSupportsBack()
    {
        ShellNavigationService navigation = new();
        navigation.Register(new ShellRoute("dashboard", ShellPage.Dashboard, "Дешборд", "Дешборд"));
        navigation.Register(new ShellRoute("assets", ShellPage.Assets, "Все активы", "Все активы"));

        ShellRoute first = navigation.Navigate("dashboard", pushHistory: false);
        ShellRoute second = navigation.Navigate("assets");
        ShellRoute back = navigation.GoBack();

        Assert(first.Route == "dashboard", "Navigation should open dashboard route.");
        Assert(second.Route == "assets", "Navigation should open assets route.");
        Assert(back.Route == "dashboard", "Back navigation should return previous route.");
    }

    private static void ShellViewModel_UpdatesActivePageAndBreadcrumb()
    {
        ShellViewModel shell = CreateShellViewModel();
        shell.Navigate("taxes");
        Assert(shell.IsTaxesPage, "Shell should activate taxes page.");
        Assert(shell.Breadcrumb == "Налоги", "Shell should update breadcrumb.");

        shell.Navigate("assets");
        shell.Navigate("assets/details");
        Assert(shell.IsAssetDetailsPage, "Shell should navigate to asset details route.");
        Assert(shell.CanGoBack, "Shell should allow back navigation after route change.");
    }

    private static async Task ShellViewModel_PreservesSelectedPortfolioAcrossNavigation()
    {
        ShellViewModel shell = CreateShellViewModel();
        await shell.InitializeAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Test User", "test", UserRole.PrivateInvestor).ConfigureAwait(false);
        PortfolioOption second = shell.PortfolioOptions.Skip(1).First();
        shell.SelectedPortfolio = second;

        shell.Navigate("goals");
        Assert(shell.SelectedPortfolio == second, "Selected portfolio must persist while switching pages.");

        shell.OpenCreatePortfolioDialog();
        shell.NewPortfolioName = "Новый портфель";
        shell.NewPortfolioCurrency = "EUR";
        await shell.CreatePortfolioAsync().ConfigureAwait(false);

        PortfolioOption? selected = shell.SelectedPortfolio;
        Assert(selected is not null, "Newly created portfolio must become selected.");
        Assert(selected!.Name == "Новый портфель", "Created portfolio should be selected.");
    }

    private static ShellViewModel CreateShellViewModel(IQuoteRefreshService? quoteService = null, ITaxCalculator? taxCalculator = null)
    {
        return new ShellViewModel(
            new ShellNavigationService(),
            new TestPortfolioService(),
            new TestAssetService(),
            new TestTransactionService(),
            new TestImportService(),
            quoteService ?? new TestQuoteRefreshService(),
            new TestGoalService(),
            taxCalculator ?? new TestTaxCalculator(),
            new TestSettingsService(),
            new TestSnapshotService(),
            new TestReportService());
    }

    private static async Task ShellViewModel_FiltersAndSortsAssets()
    {
        ShellViewModel shell = CreateShellViewModel();
        await shell.InitializeAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Test User", "test", UserRole.PrivateInvestor).ConfigureAwait(false);

        shell.AssetSearchQuery = "tech";
        Assert(shell.FilteredAssets.Count == 1, "Search should filter by tags.");

        shell.AssetSearchQuery = string.Empty;
        shell.AssetSort = "name_asc";
        Assert(shell.FilteredAssets.Count >= 2, "Asset list should load for selected portfolio.");
        Assert(string.Compare(shell.FilteredAssets[0].Name, shell.FilteredAssets[1].Name, StringComparison.OrdinalIgnoreCase) <= 0, "Sort by name must be ascending.");
    }

    private static async Task ShellViewModel_LoadsAndFiltersTransactions()
    {
        ShellViewModel shell = CreateShellViewModel();
        await shell.InitializeAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Test User", "test", UserRole.PrivateInvestor).ConfigureAwait(false);

        shell.TransactionSearchQuery = "Broker A";
        Assert(shell.FilteredTransactions.Count == 1, "Transaction search should filter by broker.");

        shell.TransactionSearchQuery = string.Empty;
        shell.TransactionSort = "amount_desc";
        Assert(shell.FilteredTransactions.Count >= 2, "Transaction list should load for selected portfolio.");
        Assert(shell.FilteredTransactions[0].GrossAmount >= shell.FilteredTransactions[1].GrossAmount, "Amount sort must be descending.");
    }

    private static async Task ShellViewModel_ComputesDashboardCards()
    {
        ShellViewModel shell = CreateShellViewModel();
        await shell.InitializeAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Test User", "test", UserRole.PrivateInvestor).ConfigureAwait(false);

        Assert(!string.IsNullOrWhiteSpace(shell.DashboardTotalValue), "Dashboard total value should be calculated.");
        Assert(shell.DashboardAllocations.Count > 0, "Dashboard allocation should be populated.");
        Assert(shell.DashboardLatestTransactions.Count > 0, "Dashboard latest transactions should be populated.");
    }

    private static async Task ShellViewModel_BuildsAssetDetailsMetrics()
    {
        ShellViewModel shell = CreateShellViewModel();
        await shell.InitializeAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Test User", "test", UserRole.PrivateInvestor).ConfigureAwait(false);
        shell.SelectedAsset = shell.FilteredAssets.First();
        shell.OpenAssetDetails();

        Assert(shell.AssetDetailsBaseMetrics.Count > 0, "Asset details base metrics should be built.");
        Assert(shell.AssetDetailsAdvancedMetrics.Count > 0, "Asset details advanced metrics should be built.");
        Assert(shell.AssetDetailsTransactions.All(item => item.AssetId == shell.SelectedAsset!.Id), "Asset details transactions must be scoped to selected asset.");
    }

    private static async Task ShellViewModel_HandlesQuoteRefreshFailureGracefully()
    {
        ShellViewModel shell = CreateShellViewModel(quoteService: new FailingQuoteRefreshService());
        await shell.InitializeAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Test User", "test", UserRole.PrivateInvestor).ConfigureAwait(false);

        await shell.RefreshQuotesAsync().ConfigureAwait(false);

        Assert(shell.QuotesStatusText.Contains("Ошибка обновления котировок", StringComparison.Ordinal), "Quote failure should be shown as user-visible status.");
        Assert(shell.Notifications.Count > 0, "Quote failure should create notification.");
    }

    private static async Task ShellViewModel_HandlesTaxRecalcFailureGracefully()
    {
        ShellViewModel shell = CreateShellViewModel(taxCalculator: new FailingTaxCalculator());
        await shell.InitializeAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Test User", "test", UserRole.PrivateInvestor).ConfigureAwait(false);

        await shell.RecalculateTaxesAsync().ConfigureAwait(false);

        Assert(shell.TaxMessage.Contains("Ошибка расчета налогов", StringComparison.Ordinal), "Tax recalculation failure should be user-visible.");
        Assert(shell.TaxExchangeRateStatus == "Источник курса: unavailable", "Tax rate source should fallback to unavailable on failure.");
        Assert(shell.Notifications.Count > 0, "Tax failure should create notification.");
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

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class TestAuthService(bool needsSetup) : ILocalAuthService
    {
        public Task<bool> NeedsFirstRunSetupAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(needsSetup);
        }

        public Task<AuthResult> CreateProfileAsync(CreateProfileRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LocalUserProfile profile = new(
                Guid.NewGuid(),
                request.DisplayName,
                request.Login,
                request.Role,
                new PasswordCredential("test", [1], [2], 1, 1),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                0);
            return Task.FromResult(AuthResult.Success(profile));
        }

        public Task<AuthResult> UnlockAsync(string login, string password, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LocalUserProfile profile = new(
                Guid.NewGuid(),
                "Demo",
                login,
                UserRole.PrivateInvestor,
                new PasswordCredential("test", [1], [2], 1, 1),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                0);
            return Task.FromResult(AuthResult.Success(profile));
        }
    }

    private sealed class TestPortfolioService : IPortfolioService
    {
        private readonly List<Portfolio> _items =
        [
            new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Guid.Parse("11111111-1111-1111-1111-111111111111"), "Личный портфель", "USD", null, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Guid.Parse("11111111-1111-1111-1111-111111111111"), "Дивиденды BYN", "BYN", null, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
        ];

        public Task<IReadOnlyList<Portfolio>> ListActiveAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<Portfolio>>(_items.Where(item => item.OwnerUserId == ownerUserId && !item.IsArchived).ToArray());
        }

        public Task<PortfolioOperationResult> CreateAsync(CreatePortfolioRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Portfolio portfolio = new(Guid.NewGuid(), request.OwnerUserId, request.Name, request.BaseCurrency, request.Description, request.ClientLabel, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            _items.Add(portfolio);
            return Task.FromResult(PortfolioOperationResult.Success(portfolio));
        }

        public Task<PortfolioOperationResult> UpdateAsync(UpdatePortfolioRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Portfolio existing = _items.First(item => item.Id == request.PortfolioId);
            Portfolio updated = existing with
            {
                Name = request.Name,
                BaseCurrency = request.BaseCurrency,
                Description = request.Description,
                ClientLabel = request.ClientLabel,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            _items[_items.FindIndex(item => item.Id == request.PortfolioId)] = updated;
            return Task.FromResult(PortfolioOperationResult.Success(updated));
        }

        public Task<PortfolioOperationResult> ArchiveAsync(Guid ownerUserId, Guid portfolioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Portfolio existing = _items.First(item => item.Id == portfolioId);
            Portfolio archived = existing with { IsArchived = true, UpdatedAt = DateTimeOffset.UtcNow };
            _items[_items.FindIndex(item => item.Id == portfolioId)] = archived;
            return Task.FromResult(PortfolioOperationResult.Success(archived));
        }
    }

    private sealed class TestAssetService : IAssetService
    {
        private readonly List<Asset> _items =
        [
            new(Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"), Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "AAPL", "Apple", AssetType.Stock, "USD", null, null, ["tech"], null, 10, 150, 190, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new(Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111"), Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "BND", "US Bond ETF", AssetType.Etf, "USD", null, null, ["bonds"], null, 15, 70, 73, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
        ];

        public Task<IReadOnlyList<Asset>> ListActiveAsync(Guid portfolioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<Asset>>(_items.Where(item => item.PortfolioId == portfolioId && !item.IsArchived).ToArray());
        }

        public Task<AssetOperationResult> CreateAsync(CreateAssetRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Asset created = new(Guid.NewGuid(), request.PortfolioId, request.Ticker.Trim().ToUpperInvariant(), request.Name, request.Type, request.Currency.ToUpperInvariant(), request.Exchange, request.Isin, request.Tags ?? [], null, request.Quantity, request.AverageBuyPrice, request.CurrentPrice, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            _items.Add(created);
            return Task.FromResult(AssetOperationResult.Success(created));
        }

        public Task<AssetOperationResult> UpdateAsync(UpdateAssetRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Asset existing = _items.First(item => item.Id == request.AssetId);
            Asset updated = existing with { Name = request.Name, Ticker = request.Ticker.Trim().ToUpperInvariant(), UpdatedAt = DateTimeOffset.UtcNow };
            _items[_items.FindIndex(item => item.Id == request.AssetId)] = updated;
            return Task.FromResult(AssetOperationResult.Success(updated));
        }

        public Task<AssetOperationResult> ArchiveAsync(Guid portfolioId, Guid assetId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Asset existing = _items.First(item => item.PortfolioId == portfolioId && item.Id == assetId);
            Asset archived = existing with { IsArchived = true, UpdatedAt = DateTimeOffset.UtcNow };
            _items[_items.FindIndex(item => item.Id == assetId)] = archived;
            return Task.FromResult(AssetOperationResult.Success(archived));
        }
    }

    private sealed class TestTransactionService : ITransactionService
    {
        private readonly List<PortfolioTransaction> _items =
        [
            new(Guid.Parse("aaaaaaaa-2222-2222-2222-222222222222"), Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"), TransactionType.Buy, DateTimeOffset.UtcNow.AddDays(-2), 2m, 120m, 240m, 1m, 0m, "USD", "Broker A", null, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new(Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222"), Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111"), TransactionType.Dividend, DateTimeOffset.UtcNow.AddDays(-1), 0m, 0m, 50m, 0m, 5m, "USD", "Broker B", null, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
        ];

        public Task<IReadOnlyList<PortfolioTransaction>> ListActiveAsync(Guid portfolioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<PortfolioTransaction>>(_items.Where(item => item.PortfolioId == portfolioId && !item.IsArchived).ToArray());
        }

        public Task<TransactionOperationResult> CreateAsync(CreateTransactionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PortfolioTransaction created = new(Guid.NewGuid(), request.PortfolioId, request.AssetId, request.Type, request.TradeDate, request.Quantity, request.Price, request.GrossAmount, request.FeeAmount, request.TaxAmount, request.Currency, request.Broker, request.ExternalId, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            _items.Add(created);
            return Task.FromResult(TransactionOperationResult.Success(created));
        }

        public Task<TransactionOperationResult> UpdateAsync(UpdateTransactionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PortfolioTransaction existing = _items.First(item => item.Id == request.TransactionId);
            PortfolioTransaction updated = existing with { GrossAmount = request.GrossAmount, UpdatedAt = DateTimeOffset.UtcNow };
            _items[_items.FindIndex(item => item.Id == request.TransactionId)] = updated;
            return Task.FromResult(TransactionOperationResult.Success(updated));
        }

        public Task<TransactionOperationResult> ArchiveAsync(Guid portfolioId, Guid transactionId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PortfolioTransaction existing = _items.First(item => item.PortfolioId == portfolioId && item.Id == transactionId);
            PortfolioTransaction archived = existing with { IsArchived = true, UpdatedAt = DateTimeOffset.UtcNow };
            _items[_items.FindIndex(item => item.Id == transactionId)] = archived;
            return Task.FromResult(TransactionOperationResult.Success(archived));
        }
    }

    private sealed class TestImportService : IImportService
    {
        public Task<ImportPreview> PreviewAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ImportPreview(true, string.Empty, [], false));
        }
    }

    private sealed class TestQuoteRefreshService : IQuoteRefreshService
    {
        public Task<QuoteRefreshSummary> RefreshPortfolioAsync(Guid portfolioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new QuoteRefreshSummary(1, 0, 0, "Quotes: updated=1, cached=0, failed=0"));
        }
    }

    private sealed class FailingQuoteRefreshService : IQuoteRefreshService
    {
        public Task<QuoteRefreshSummary> RefreshPortfolioAsync(Guid portfolioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("quote provider offline");
        }
    }

    private sealed class TestGoalService : IGoalService
    {
        public Task<IReadOnlyList<Goal>> ListActiveAsync(Guid portfolioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<Goal> goals =
            [
                new(Guid.Parse("aaaaaaaa-3333-3333-3333-333333333333"), portfolioId, "FIRE", 100000m, "USD", 1000m, 8m, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            ];
            return Task.FromResult(goals);
        }

        public Task<GoalOperationResult> CreateAsync(CreateGoalRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Goal goal = new(Guid.NewGuid(), request.PortfolioId, request.Title, request.TargetAmount, request.Currency, request.MonthlyContribution, request.ExpectedAnnualReturnPercent, request.TargetDate, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            return Task.FromResult(GoalOperationResult.Success(goal));
        }

        public Task<GoalOperationResult> UpdateAsync(UpdateGoalRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Goal goal = new(request.GoalId, request.PortfolioId, request.Title, request.TargetAmount, request.Currency, request.MonthlyContribution, request.ExpectedAnnualReturnPercent, request.TargetDate, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            return Task.FromResult(GoalOperationResult.Success(goal));
        }

        public Task<GoalOperationResult> ArchiveAsync(Guid portfolioId, Guid goalId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Goal goal = new(goalId, portfolioId, "Archived", 1m, "USD", 0m, null, null, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            return Task.FromResult(GoalOperationResult.Success(goal));
        }

        public GoalForecast Forecast(Goal goal, decimal currentPortfolioValue)
        {
            return new GoalForecast(true, 24, DateTimeOffset.UtcNow.AddMonths(24), goal.TargetAmount, "ok");
        }
    }

    private sealed class TestTaxCalculator : ITaxCalculator
    {
        public Task<TaxCalculationResult> CalculateAsync(IReadOnlyList<TaxTransactionSnapshot> transactions, int reportYear, LegalProfileType profile, string baseCurrency, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new TaxCalculationResult(
                true,
                "Черновой/информационный расчёт.",
                123m,
                1000m,
                200m,
                400m,
                50m,
                10m,
                7m,
                -20m,
                "mock-nbrb",
                DateOnly.FromDateTime(DateTime.UtcNow),
                new TaxRuleSet("BY-DRAFT-TEST", DateOnly.FromDateTime(DateTime.UtcNow), 13m, 13m, 200m, "Draft / informational")));
        }
    }

    private sealed class FailingTaxCalculator : ITaxCalculator
    {
        public Task<TaxCalculationResult> CalculateAsync(IReadOnlyList<TaxTransactionSnapshot> transactions, int reportYear, LegalProfileType profile, string baseCurrency, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("tax provider offline");
        }
    }

    private sealed class TestSettingsService : ISettingsService
    {
        private UserSettings? _settings;

        public Task<UserSettings> EnsureAsync(CreateDefaultSettingsRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _settings ??= new UserSettings(
                request.OwnerUserId,
                request.DisplayName,
                request.Role,
                request.Login,
                request.PreferredCurrency,
                AppLanguage.RU,
                1m,
                QuoteProviderKind.Mock,
                15,
                string.Empty,
                CurrencyProviderKind.Mock,
                false,
                null);
            return Task.FromResult(_settings);
        }

        public Task<UserSettings?> GetAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_settings);
        }

        public Task<SettingsOperationResult> UpdateAsync(UpdateSettingsRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _settings = new UserSettings(
                request.OwnerUserId,
                request.DisplayName,
                request.Role,
                "test",
                request.PreferredCurrency,
                request.Language,
                request.UiScale,
                request.QuoteProvider,
                request.QuoteRefreshMinutes,
                string.IsNullOrWhiteSpace(request.FinnhubApiKeyRaw) ? string.Empty : "protected",
                request.CurrencyProvider,
                request.SyncEnabled,
                DateTimeOffset.UtcNow);
            return Task.FromResult(SettingsOperationResult.Success(_settings));
        }
    }

    private sealed class TestSnapshotService : ISnapshotService
    {
        public Task<SnapshotExportResult> ExportAsync(string password, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new SnapshotExportResult(true, "ok", "/tmp/mock.pxsnap", DateTimeOffset.UtcNow));
        }

        public Task<SnapshotPreviewResult> PreviewImportAsync(string snapshotPath, string password, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new SnapshotPreviewResult(true, "ok", "1.0.0", 1, DateTimeOffset.UtcNow, "dev", SnapshotConflictKind.None));
        }

        public Task<SnapshotImportResult> ImportAsync(string snapshotPath, string password, bool allowConflictOverride, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new SnapshotImportResult(true, "ok", SnapshotConflictKind.None));
        }
    }

    private sealed class TestReportService : IReportService
    {
        public ReportPreviewResult PreviewPortfolio(PortfolioReportRequest request) => new(true, "ok", ["summary"]);
        public ReportPreviewResult PreviewTax(TaxReportRequest request) => new(true, "ok", ["summary"]);
        public Task<ReportExportResult> ExportPortfolioPdfAsync(PortfolioReportRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ReportExportResult(true, "ok", "/tmp/p.pdf"));
        }
        public Task<ReportExportResult> ExportTaxPdfAsync(TaxReportRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ReportExportResult(true, "ok", "/tmp/t.pdf"));
        }
    }
}

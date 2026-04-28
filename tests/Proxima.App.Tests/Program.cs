using Proxima.Analytics;
using Proxima.App.Controls;
using Proxima.App.ViewModels;
using Proxima.Application;
using Proxima.Application.Assets;
using Proxima.Application.Auth;
using Proxima.Application.Portfolios;
using Proxima.Domain.Assets;
using Proxima.Domain;
using Proxima.Domain.Auth;
using Proxima.Domain.Portfolios;
using Proxima.Importing;
using Proxima.Infrastructure;
using Proxima.Reporting;
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
        Console.WriteLine("Proxima.App.Tests baseline checks passed.");
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
        await shell.InitializeAsync(Guid.Parse("11111111-1111-1111-1111-111111111111")).ConfigureAwait(false);
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

    private static ShellViewModel CreateShellViewModel()
    {
        return new ShellViewModel(new ShellNavigationService(), new TestPortfolioService(), new TestAssetService());
    }

    private static async Task ShellViewModel_FiltersAndSortsAssets()
    {
        ShellViewModel shell = CreateShellViewModel();
        await shell.InitializeAsync(Guid.Parse("11111111-1111-1111-1111-111111111111")).ConfigureAwait(false);

        shell.AssetSearchQuery = "tech";
        Assert(shell.FilteredAssets.Count == 1, "Search should filter by tags.");

        shell.AssetSearchQuery = string.Empty;
        shell.AssetSort = "name_asc";
        Assert(shell.FilteredAssets.Count >= 2, "Asset list should load for selected portfolio.");
        Assert(string.Compare(shell.FilteredAssets[0].Name, shell.FilteredAssets[1].Name, StringComparison.OrdinalIgnoreCase) <= 0, "Sort by name must be ascending.");
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
}

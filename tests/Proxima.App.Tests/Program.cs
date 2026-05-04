using Proxima.Analytics;
using Proxima.App.Navigation;
using Proxima.Application;
using Proxima.Application.Observability;
using Proxima.Domain;
using Proxima.Infrastructure;
using Proxima.Importing;
using Proxima.Reporting;
using Proxima.Reporting.Reports;
using Proxima.Sync;
using Proxima.Sync.Snapshots;

namespace Proxima.App.Tests;

internal static class Program
{
    private static void Main()
    {
        AssemblyNames_AreStable();
        Domain_HasNoForbiddenDependencies();
        DesignSystem_FilesExist();
        RuntimeAuthFlow_UsesLoginThenAppShell();
        RuntimeAuthFlow_UsesProfileBackedUserContext();
        RuntimeRoutes_ExistForMigratedShellScreens();
        RuntimeViews_ContainExpectedChartAndSecurityElements();
        RedactionHelper_RedactsSensitiveFragments();
        SnapshotService_EncryptDecryptAndTamperFail().GetAwaiter().GetResult();
        ReportingService_ExportsNonEmptyPdf().GetAwaiter().GetResult();
        Console.WriteLine("Proxima.App.Tests baseline checks passed.");
    }

    private static void RuntimeAuthFlow_UsesLoginThenAppShell()
    {
        string root = FindRepositoryRoot();
        string appCode = File.ReadAllText(Path.Combine(root, "src", "Proxima.App", "App.axaml.cs"));
        string loginView = File.ReadAllText(Path.Combine(root, "src", "Proxima.App", "Views", "Auth", "LoginView.axaml"));

        Assert(appCode.Contains("LoginViewModel", StringComparison.Ordinal), "Runtime should initialize LoginViewModel.");
        Assert(appCode.Contains("IRuntimeAuthBootstrapper", StringComparison.Ordinal), "Runtime should bootstrap profile-backed auth before opening shell/login.");
        Assert(appCode.Contains("CreateAppShellWindow", StringComparison.Ordinal), "Runtime should create AppShell window after unlock.");
        Assert(loginView.Contains("PasswordChar=\"•\"", StringComparison.Ordinal), "Login view password input must be masked.");
        Assert(loginView.Contains("Forgot password / Recovery", StringComparison.Ordinal), "Login view should expose recovery action.");
    }

    private static void RuntimeAuthFlow_UsesProfileBackedUserContext()
    {
        string root = FindRepositoryRoot();
        string composition = File.ReadAllText(Path.Combine(root, "src", "Proxima.App", "Composition", "AppComposition.cs"));
        string settingsVm = File.ReadAllText(Path.Combine(root, "src", "Proxima.App", "Views", "Settings", "SettingsViewModel.cs"));

        Assert(composition.Contains("LocalProfileAuthGateService", StringComparison.Ordinal), "App composition should use profile-backed auth gate service.");
        Assert(composition.Contains("IRuntimeUserContext", StringComparison.Ordinal), "Runtime user context must be registered in composition.");
        Assert(!settingsVm.Contains("RuntimeOwnerUserId", StringComparison.Ordinal), "Settings runtime path must not use hardcoded owner id.");
        Assert(settingsVm.Contains("_runtimeUserContext.UserId", StringComparison.Ordinal), "Settings should use authenticated runtime user context.");
    }

    private static void RuntimeRoutes_ExistForMigratedShellScreens()
    {
        Assert(AppRoutes.Dashboard == "dashboard", "Dashboard route key mismatch.");
        Assert(AppRoutes.Assets == "assets", "Assets route key mismatch.");
        Assert(AppRoutes.AssetDetails == "asset-details", "Asset Details route key mismatch.");
        Assert(AppRoutes.ManualImport == "assets-import-manual", "Manual Import route key mismatch.");
        Assert(AppRoutes.Goals == "goals", "Goals route key mismatch.");
        Assert(AppRoutes.Taxes == "taxes", "Taxes route key mismatch.");
        Assert(AppRoutes.Settings == "settings", "Settings route key mismatch.");

        string shellCode = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Proxima.App", "Shell", "AppShellViewModel.cs"));
        string[] requiredRouteKeys =
        [
            "AppRoutes.Dashboard",
            "AppRoutes.Assets",
            "AppRoutes.AssetDetails",
            "AppRoutes.ManualImport",
            "AppRoutes.Goals",
            "AppRoutes.Taxes",
            "AppRoutes.Settings"
        ];

        foreach (string routeKey in requiredRouteKeys)
        {
            Assert(shellCode.Contains(routeKey, StringComparison.Ordinal), $"AppShellViewModel should register {routeKey}.");
        }
    }

    private static void RuntimeViews_ContainExpectedChartAndSecurityElements()
    {
        string root = FindRepositoryRoot();
        string appShellView = File.ReadAllText(Path.Combine(root, "src", "Proxima.App", "Shell", "AppShellView.axaml"));
        string dashboardView = File.ReadAllText(Path.Combine(root, "src", "Proxima.App", "Views", "Dashboard", "DashboardView.axaml"));
        string assetDetailsView = File.ReadAllText(Path.Combine(root, "src", "Proxima.App", "Views", "AssetDetails", "AssetDetailsView.axaml"));
        string settingsView = File.ReadAllText(Path.Combine(root, "src", "Proxima.App", "Views", "Settings", "SettingsView.axaml"));

        Assert(appShellView.Contains("SidebarView", StringComparison.Ordinal), "AppShell should contain sidebar host.");
        Assert(appShellView.Contains("TopbarView", StringComparison.Ordinal), "AppShell should contain topbar host.");
        Assert(appShellView.Contains("ManualImportView", StringComparison.Ordinal), "AppShell should bind Manual Import content.");
        Assert(dashboardView.Contains("controls:ProximaCartesianChart", StringComparison.Ordinal), "Dashboard should include Proxima cartesian chart control.");
        Assert(dashboardView.Contains("controls:ProximaDonutChart", StringComparison.Ordinal), "Dashboard should include Proxima donut chart control.");
        Assert(assetDetailsView.Contains("controls:ProximaCandlestickChart", StringComparison.Ordinal), "Asset details should include Proxima candlestick chart control.");
        Assert(settingsView.Contains("Сменить пароль", StringComparison.Ordinal), "Settings should expose password change action.");
        Assert(settingsView.Contains("Экспорт снапшота", StringComparison.Ordinal), "Settings should expose snapshot export action.");
    }

    private static void RedactionHelper_RedactsSensitiveFragments()
    {
        string redacted = RedactionHelper.Redact("token=abc password=hello apiKey=xyz");
        Assert(!redacted.Contains("password", StringComparison.OrdinalIgnoreCase), "Redaction should remove password key fragments.");
        Assert(!redacted.Contains("token", StringComparison.OrdinalIgnoreCase), "Redaction should remove token key fragments.");
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
            "src/Proxima.App/DesignSystem/Charts/ProximaChartTheme.cs",
            "src/Proxima.App/DesignSystem/Charts/ProximaChartAxisFactory.cs",
            "src/Proxima.App/DesignSystem/Charts/ProximaChartTooltipFormatter.cs",
            "src/Proxima.App/DesignSystem/Components/ProximaCartesianChart.axaml",
            "src/Proxima.App/DesignSystem/Components/ProximaDonutChart.axaml",
            "src/Proxima.App/DesignSystem/Components/ProximaCandlestickChart.axaml",
        ];

        foreach (string file in requiredFiles)
        {
            Assert(File.Exists(Path.Combine(root, file)), $"Required design-system file is missing: {file}");
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

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

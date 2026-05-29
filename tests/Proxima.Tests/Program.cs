using System.Reflection;
using System.Text;
using Proxima.Core.Application.Analytics.AssetDetails;
using Microsoft.EntityFrameworkCore;
using Proxima.Core.Application.Analytics;
using Proxima.Core.Application.Importing;
using Proxima.Core.Application.Reporting;
using Proxima.Core.Domain.Auth;
using Proxima.Infrastructure.Auth;
using Proxima.Infrastructure.Importing;
using Proxima.Infrastructure.Persistence;
using Proxima.Infrastructure.Reporting;

namespace Proxima.Tests;

internal static class Program
{
    private static async Task Main()
    {
        Architecture_ProjectGraph_IsCompact();
        Architecture_Core_HasNoForbiddenReferences();
        EfModel_ContainsOnlyActiveTablesAndSingleEncodedPasswordColumn();
        PasswordHasher_StoresSelfContainedPbkdf2Credential();
        AnalyticsEngine_ComputesCorePortfolioMetrics();
        AssetDetailsCalculator_ComputesRiskMetricsInCore();
        await Importing_CsvPreviewParsesRowsWithoutSavingAnything().ConfigureAwait(false);
        await Reporting_PdfExporterProducesNonEmptyFile().ConfigureAwait(false);

        Console.WriteLine("Proxima.Tests passed.");
    }

    private static void Architecture_ProjectGraph_IsCompact()
    {
        string root = FindRepositoryRoot();
        string[] expectedProjects =
        [
            Path.Combine(root, "src", "Proxima.App", "Proxima.App.csproj"),
            Path.Combine(root, "src", "Proxima.Core", "Proxima.Core.csproj"),
            Path.Combine(root, "src", "Proxima.Infrastructure", "Proxima.Infrastructure.csproj"),
            Path.Combine(root, "tests", "Proxima.Tests", "Proxima.Tests.csproj"),
        ];

        string[] actualProjects = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .Order(StringComparer.Ordinal)
            .ToArray();

        foreach (string expectedProject in expectedProjects)
        {
            Assert(actualProjects.Contains(Path.GetFullPath(expectedProject), StringComparer.Ordinal), $"Expected project missing: {expectedProject}");
        }

        Assert(actualProjects.Length == expectedProjects.Length, "Only App, Core, Infrastructure and Proxima.Tests projects should remain.");

        string coreProject = File.ReadAllText(Path.Combine(root, "src", "Proxima.Core", "Proxima.Core.csproj"));
        string infrastructureProject = File.ReadAllText(Path.Combine(root, "src", "Proxima.Infrastructure", "Proxima.Infrastructure.csproj"));
        string appProject = File.ReadAllText(Path.Combine(root, "src", "Proxima.App", "Proxima.App.csproj"));

        Assert(!coreProject.Contains("ProjectReference", StringComparison.Ordinal), "Core must not reference other projects.");
        Assert(infrastructureProject.Contains("..\\Proxima.Core\\Proxima.Core.csproj", StringComparison.Ordinal), "Infrastructure must reference Core.");
        Assert(!infrastructureProject.Contains("Proxima.App.csproj", StringComparison.Ordinal), "Infrastructure must not reference App.");
        Assert(appProject.Contains("..\\Proxima.Core\\Proxima.Core.csproj", StringComparison.Ordinal), "App must reference Core.");
        Assert(appProject.Contains("..\\Proxima.Infrastructure\\Proxima.Infrastructure.csproj", StringComparison.Ordinal), "App must reference Infrastructure.");
    }

    private static void Architecture_Core_HasNoForbiddenReferences()
    {
        Assembly coreAssembly = typeof(PasswordCredential).Assembly;
        Assert(coreAssembly.GetName().Name == "Proxima.Core", "Domain and application core must be compiled into Proxima.Core.");

        string[] forbiddenReferences =
        [
            "Avalonia",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "PdfSharp",
        ];

        string[] references = coreAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        foreach (string forbidden in forbiddenReferences)
        {
            Assert(!references.Any(reference => reference.StartsWith(forbidden, StringComparison.Ordinal)), $"Core must not reference {forbidden}.");
        }

        string root = FindRepositoryRoot();
        foreach (string file in Directory.GetFiles(Path.Combine(root, "src", "Proxima.Core"), "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            Assert(!text.Contains("using Avalonia", StringComparison.Ordinal), $"Core file references Avalonia: {file}");
            Assert(!text.Contains("using Microsoft.EntityFrameworkCore", StringComparison.Ordinal), $"Core file references EF Core: {file}");
            Assert(!text.Contains("using Npgsql", StringComparison.Ordinal), $"Core file references Npgsql: {file}");
            Assert(!text.Contains("using PdfSharp", StringComparison.Ordinal), $"Core file references PDFSharp: {file}");
            Assert(!text.Contains("using Proxima.Infrastructure", StringComparison.Ordinal), $"Core file references Infrastructure: {file}");
            Assert(!text.Contains("using Proxima.App", StringComparison.Ordinal), $"Core file references App: {file}");
        }
    }

    private static void EfModel_ContainsOnlyActiveTablesAndSingleEncodedPasswordColumn()
    {
        DbContextOptions<ProximaDbContext> options = new DbContextOptionsBuilder<ProximaDbContext>()
            .UseInMemoryDatabase("proxima-model-" + Guid.NewGuid().ToString("N"))
            .Options;

        using ProximaDbContext db = new(options);
        string[] tableNames = db.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName() ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] expectedTables =
        [
            "asset_prices",
            "asset_tags",
            "assets",
            "audit_log",
            "goals",
            "notifications",
            "portfolios",
            "quote_cache",
            "tags",
            "transactions",
            "user_settings",
            "users",
        ];
        foreach (string table in expectedTables)
        {
            Assert(tableNames.Contains(table, StringComparer.Ordinal), $"Active table must be mapped: {table}");
        }

        var userEntity = db.Model.GetEntityTypes().Single(entity => entity.GetTableName() == "users");
        string[] userColumns = userEntity.GetProperties().Select(property => property.GetColumnName()).ToArray();
        Assert(userColumns.Contains("password_hash", StringComparer.Ordinal), "users.password_hash must exist.");
    }

    private static void PasswordHasher_StoresSelfContainedPbkdf2Credential()
    {
        Pbkdf2PasswordHasher hasher = new();
        PasswordCredential first = hasher.Hash("Proxima2026!");
        PasswordCredential second = hasher.Hash("Proxima2026!");

        Assert(first.EncodedHash.StartsWith("$pbkdf2-sha256$", StringComparison.Ordinal), "Hash must use PBKDF2-SHA256 format marker.");
        Assert(first.EncodedHash.Contains("$v=1$", StringComparison.Ordinal), "Hash must contain version metadata.");
        Assert(first.EncodedHash.Contains("$i=210000$", StringComparison.Ordinal), "Hash must contain iteration metadata.");
        Assert(first.EncodedHash != second.EncodedHash, "Same password must produce different hashes because salt is random.");
        Assert(hasher.Verify("Proxima2026!", first), "Correct password must verify.");
        Assert(!hasher.Verify("WrongPassword", first), "Wrong password must not verify.");
        Assert(!hasher.Verify("anything", new PasswordCredential("")), "Empty credential must be rejected safely.");
        Assert(!hasher.Verify("anything", new PasswordCredential("plain-sha256-like-value")), "Legacy/plain malformed credential must be rejected safely.");
        Assert(!hasher.Verify("anything", new PasswordCredential("$pbkdf2-sha256$v=1$i=abc$salt$hash")), "Malformed credential must not crash.");
    }

    private static void AnalyticsEngine_ComputesCorePortfolioMetrics()
    {
        PositionInput[] positions =
        [
            new(2m, 100m, 120m, 1m),
            new(1m, 50m, 70m, 0m),
        ];

        Assert(PortfolioMetricsCalculator.TotalValue(positions, 10m) == 320m, "Total value should include positions and cash.");
        Assert(PortfolioMetricsCalculator.UnrealizedPnl(positions[0]) == 39m, "PnL should include fees.");
        Assert(PortfolioMetricsCalculator.Roi(100m, 20m) == 20m, "ROI should calculate percentage return.");
        Assert(PortfolioMetricsCalculator.Roi(0m, 20m) is null, "ROI should be unavailable for zero denominator.");
        Assert(PortfolioMetricsCalculator.MaxDrawdown([100d, 95d, 110d, 90d]).Availability == MetricAvailability.Available, "Max drawdown should compute.");
        Assert(PortfolioMetricsCalculator.Volatility([0.01d, -0.02d, 0.015d]).Availability == MetricAvailability.Available, "Volatility should compute.");
        Assert(PortfolioMetricsCalculator.Sharpe([0.01d, -0.02d, 0.015d]).Availability == MetricAvailability.Available, "Sharpe should compute.");
    }

    private static void AssetDetailsCalculator_ComputesRiskMetricsInCore()
    {
        decimal[] closes = [100m, 104m, 102m, 108m, 111m, 109m, 115m];
        decimal[] highs = [101m, 105m, 103m, 109m, 112m, 110m, 116m];
        decimal[] lows = [99m, 102m, 100m, 106m, 108m, 107m, 112m];

        AssetDetailsAnalyticsResult result = AssetDetailsCalculator.Calculate(closes, highs, lows, 115m, 1.2m);

        Assert(result.Rsi > 50m, "Rising closes should produce RSI above neutral.");
        Assert(result.Atr > 0m, "ATR should be available for OHLC data.");
        Assert(result.Beta == 1.2m, "Provider beta should pass through analytics result.");
        Assert(!string.IsNullOrWhiteSpace(result.SmaStatus), "SMA status should be user-visible.");
    }

    private static async Task Importing_CsvPreviewParsesRowsWithoutSavingAnything()
    {
        string directory = Path.Combine(Path.GetTempPath(), "proxima-import-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "transactions.csv");
        string csv = "date,ticker,name,type,quantity,price,currency,fee,broker,tag\n2026-01-01,AAPL,Apple,Buy,2,100,USD,1,Broker,tech";
        await File.WriteAllTextAsync(path, csv, Encoding.UTF8).ConfigureAwait(false);

        IImportService importService = new ImportService(
            new ImportFileValidator(5 * 1024 * 1024),
            [new CsvImportParser()]);

        ImportPreview preview = await importService.PreviewAsync(path, CancellationToken.None).ConfigureAwait(false);
        Assert(preview.Succeeded, "CSV preview should succeed.");
        Assert(preview.Rows.Count == 1, "CSV preview should return one parsed row.");
        Assert(preview.Rows[0].Status == ImportRowStatus.Valid, "Valid CSV row should remain valid.");
    }

    private static async Task Reporting_PdfExporterProducesNonEmptyFile()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), "proxima-report-tests", Guid.NewGuid().ToString("N"));
        IReportService service = new PdfReportService();
        PortfolioReportRequest request = new(
            PortfolioName: "Demo Portfolio",
            PeriodLabel: "2026",
            TotalValue: 10_000m,
            ProfitLoss: 750m,
            Allocation: [("Stocks", 7_000m), ("Cash", 3_000m)],
            TopAssets: [("AAPL", 4_000m), ("MSFT", 3_000m)],
            RiskMetrics: [("Sharpe", "1.20"), ("MDD", "-8%")],
            TransactionCount: 5,
            Currency: "USD",
            Disclaimer: "Test report.",
            OutputDirectory: outputDirectory);

        ReportExportResult result = await service.ExportPortfolioPdfAsync(request, CancellationToken.None).ConfigureAwait(false);
        Assert(result.Succeeded, result.Message);
        string outputPath = result.OutputPath ?? throw new InvalidOperationException("PDF exporter did not return an output path.");
        Assert(File.Exists(outputPath), "PDF exporter should create a file.");
        Assert(new FileInfo(outputPath).Length > 0, "PDF exporter should create a non-empty file.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Proxima.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

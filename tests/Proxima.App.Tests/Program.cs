using Proxima.Analytics;
using Proxima.App.Controls;
using Proxima.Application;
using Proxima.Domain;
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

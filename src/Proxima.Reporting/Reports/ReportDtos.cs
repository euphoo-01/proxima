namespace Proxima.Reporting.Reports;

public sealed record PortfolioReportRequest(
    string PortfolioName,
    string PeriodLabel,
    decimal TotalValue,
    decimal ProfitLoss,
    IReadOnlyList<(string Category, decimal Value)> Allocation,
    IReadOnlyList<(string Asset, decimal Value)> TopAssets,
    IReadOnlyList<(string Metric, string Value)> RiskMetrics,
    int TransactionCount,
    string Currency,
    string Disclaimer,
    string OutputDirectory);

public sealed record TaxReportRequest(
    string UserDisplayName,
    string TaxProfile,
    int Year,
    decimal TaxableBase,
    decimal TotalTaxDue,
    string ExchangeRateNotes,
    decimal Dividends,
    int TransactionCount,
    string CalculationVersion,
    string LegalDisclaimer,
    string OutputDirectory);

public sealed record ReportPreviewResult(bool Succeeded, string Message, IReadOnlyList<string> Sections);
public sealed record ReportExportResult(bool Succeeded, string Message, string? OutputPath);

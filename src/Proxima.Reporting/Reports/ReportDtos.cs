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
    string TaxProfileDescription,
    int Year,
    decimal TaxableBase,
    decimal TotalTaxDue,
    decimal TaxSaved,
    decimal Dividends,
    decimal RealizedGains,
    decimal Fees,
    decimal Losses,
    decimal CurrencyEffect,
    decimal BaseRatePercent,
    decimal DividendRatePercent,
    decimal IncomeThreshold,
    int TransactionCount,
    string Currency,
    string ExchangeRateNotes,
    string CalculationVersion,
    string LegalDisclaimer,
    IReadOnlyList<TaxReportLine> CalculationBreakdown,
    IReadOnlyList<TaxReportLine> TaxBreakdown,
    string OutputDirectory);

public sealed record TaxReportLine(string Label, decimal Amount, string Note, string Kind);

public sealed record ReportPreviewResult(bool Succeeded, string Message, IReadOnlyList<string> Sections);
public sealed record ReportExportResult(bool Succeeded, string Message, string? OutputPath);

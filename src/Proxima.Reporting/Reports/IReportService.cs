namespace Proxima.Reporting.Reports;

public interface IReportService
{
    ReportPreviewResult PreviewPortfolio(PortfolioReportRequest request);

    ReportPreviewResult PreviewTax(TaxReportRequest request);

    Task<ReportExportResult> ExportPortfolioPdfAsync(PortfolioReportRequest request, CancellationToken cancellationToken = default);

    Task<ReportExportResult> ExportTaxPdfAsync(TaxReportRequest request, CancellationToken cancellationToken = default);
}

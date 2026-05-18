namespace Proxima.Core.Application.Taxes;

public interface ITaxService
{
    Task<TaxOverview> GetOverviewAsync(
        Guid portfolioId,
        Guid userId,
        int year,
        CancellationToken cancellationToken = default);

    Task<TaxExportResult> ExportPdfAsync(
        Guid portfolioId,
        string portfolioName,
        Guid userId,
        int year,
        CancellationToken cancellationToken = default);
}

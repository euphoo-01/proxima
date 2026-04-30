using System.Globalization;
using System.Text;

namespace Proxima.Reporting.Reports;

public sealed class SimplePdfReportService : IReportService
{
    public ReportPreviewResult PreviewPortfolio(PortfolioReportRequest request)
    {
        if (!ValidateOutputDirectory(request.OutputDirectory, out string message))
        {
            return new ReportPreviewResult(false, message, []);
        }

        List<string> sections =
        [
            "Portfolio Summary",
            "Allocation",
            "Top Assets",
            "Risk Metrics",
            "Transaction Summary",
            "Disclaimer",
        ];

        return new ReportPreviewResult(true, "Preview ready.", sections);
    }

    public ReportPreviewResult PreviewTax(TaxReportRequest request)
    {
        if (!ValidateOutputDirectory(request.OutputDirectory, out string message))
        {
            return new ReportPreviewResult(false, message, []);
        }

        List<string> sections =
        [
            "Tax Profile Summary",
            "Tax Base and Due",
            "Exchange Rate Notes",
            "Dividends",
            "Transaction Summary",
            "Calculation Version",
            "Legal Disclaimer",
        ];

        return new ReportPreviewResult(true, "Preview ready.", sections);
    }

    public async Task<ReportExportResult> ExportPortfolioPdfAsync(PortfolioReportRequest request, CancellationToken cancellationToken = default)
    {
        if (!ValidateOutputDirectory(request.OutputDirectory, out string message))
        {
            return new ReportExportResult(false, message, null);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(request.OutputDirectory);
            string path = Path.Combine(request.OutputDirectory, BuildFileName("portfolio-report", request.PortfolioName));

            List<string> lines =
            [
                "Proxima Portfolio Report",
                $"Portfolio: {request.PortfolioName}",
                $"Period: {request.PeriodLabel}",
                $"Total Value: {request.TotalValue.ToString("0.##", CultureInfo.InvariantCulture)} {request.Currency}",
                $"P&L: {request.ProfitLoss.ToString("0.##", CultureInfo.InvariantCulture)} {request.Currency}",
                $"Transactions: {request.TransactionCount}",
                "Allocation:",
            ];
            lines.AddRange(request.Allocation.Select(item => $"- {item.Category}: {item.Value.ToString("0.##", CultureInfo.InvariantCulture)}"));
            lines.Add("Top assets:");
            lines.AddRange(request.TopAssets.Select(item => $"- {item.Asset}: {item.Value.ToString("0.##", CultureInfo.InvariantCulture)}"));
            lines.Add("Risk metrics:");
            lines.AddRange(request.RiskMetrics.Select(item => $"- {item.Metric}: {item.Value}"));
            lines.Add($"Generated at (UTC): {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}");
            lines.Add($"Disclaimer: {request.Disclaimer}");

            await WritePdfAsync(path, lines, cancellationToken).ConfigureAwait(false);
            return new ReportExportResult(true, "Portfolio report exported.", path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ReportExportResult(false, $"Portfolio export failed: {ex.Message}", null);
        }
    }

    public async Task<ReportExportResult> ExportTaxPdfAsync(TaxReportRequest request, CancellationToken cancellationToken = default)
    {
        if (!ValidateOutputDirectory(request.OutputDirectory, out string message))
        {
            return new ReportExportResult(false, message, null);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(request.OutputDirectory);
            string path = Path.Combine(request.OutputDirectory, BuildFileName("tax-draft-report", $"{request.UserDisplayName}-{request.Year}"));

            List<string> lines =
            [
                "Proxima Tax Draft Report",
                $"User: {request.UserDisplayName}",
                $"Profile: {request.TaxProfile}",
                $"Year: {request.Year}",
                $"Taxable base: {request.TaxableBase.ToString("0.##", CultureInfo.InvariantCulture)}",
                $"Total tax due: {request.TotalTaxDue.ToString("0.##", CultureInfo.InvariantCulture)}",
                $"Dividends: {request.Dividends.ToString("0.##", CultureInfo.InvariantCulture)}",
                $"Transactions: {request.TransactionCount}",
                $"Rate notes: {request.ExchangeRateNotes}",
                $"Calculation version: {request.CalculationVersion}",
                $"Generated at (UTC): {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}",
                $"Disclaimer: {request.LegalDisclaimer}",
            ];

            await WritePdfAsync(path, lines, cancellationToken).ConfigureAwait(false);
            return new ReportExportResult(true, "Tax draft report exported.", path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ReportExportResult(false, $"Tax export failed: {ex.Message}", null);
        }
    }

    private static async Task WritePdfAsync(string path, IReadOnlyList<string> lines, CancellationToken cancellationToken)
    {
        string contentStream = BuildContentStream(lines);
        byte[] pdf = BuildPdf(contentStream);
        await File.WriteAllBytesAsync(path, pdf, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildFileName(string prefix, string identity)
    {
        string safe = new string(identity.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "report";
        }

        return $"{prefix}-{safe}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
    }

    private static bool ValidateOutputDirectory(string outputDirectory, out string message)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            message = "Output directory is required.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static string BuildContentStream(IReadOnlyList<string> lines)
    {
        StringBuilder builder = new();
        builder.AppendLine("BT");
        builder.AppendLine("/F1 11 Tf");
        builder.AppendLine("50 790 Td");

        bool first = true;
        foreach (string line in lines)
        {
            string escaped = line.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            if (first)
            {
                builder.AppendLine($"({escaped}) Tj");
                first = false;
            }
            else
            {
                builder.AppendLine("0 -14 Td");
                builder.AppendLine($"({escaped}) Tj");
            }
        }

        builder.AppendLine("ET");
        return builder.ToString();
    }

    private static byte[] BuildPdf(string contentStream)
    {
        List<string> objects =
        [
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Count 1 /Kids [3 0 R] >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n",
            "4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n",
            $"5 0 obj\n<< /Length {Encoding.ASCII.GetByteCount(contentStream)} >>\nstream\n{contentStream}endstream\nendobj\n",
        ];

        StringBuilder body = new();
        List<int> offsets = [0];
        body.Append("%PDF-1.4\n");
        foreach (string obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(body.ToString()));
            body.Append(obj);
        }

        int xrefStart = Encoding.ASCII.GetByteCount(body.ToString());
        body.Append("xref\n");
        body.Append($"0 {objects.Count + 1}\n");
        body.Append("0000000000 65535 f \n");
        for (int i = 1; i < offsets.Count; i++)
        {
            body.Append($"{offsets[i].ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n");
        }

        body.Append("trailer\n");
        body.Append($"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        body.Append("startxref\n");
        body.Append($"{xrefStart}\n");
        body.Append("%%EOF");
        return Encoding.ASCII.GetBytes(body.ToString());
    }
}

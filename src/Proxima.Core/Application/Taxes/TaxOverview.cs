namespace Proxima.Core.Application.Taxes;

public enum TaxBreakdownKind
{
    Income,
    Currency,
    Deduction,
    Benefit,
    Info,
}

public sealed record TaxProfileDescriptor(string Name, string Description, string TaxBaseNote);

public sealed record TaxOverview(
    bool IsEmpty,
    string Message,
    string Status,
    string Currency,
    decimal TaxableBase,
    decimal TotalTaxDue,
    decimal TaxSaved,
    decimal RealizedGains,
    decimal Dividends,
    decimal Fees,
    decimal CurrencyEffect,
    decimal Losses,
    decimal BaseRatePercent,
    decimal DividendRatePercent,
    decimal ExemptionAmount,
    decimal IncomeThreshold,
    int TransactionCount,
    string RateSourceText,
    string CalculationVersion,
    string ProfileName,
    string ProfileDescription,
    bool IsOfflineRate,
    string OfflineRateMessage,
    IReadOnlyList<TaxBreakdownRow> Breakdown,
    IReadOnlyList<TaxBreakdownRow> TaxBreakdown,
    string LegalDisclaimer)
{
    public static TaxOverview Empty(string message, TaxProfileDescriptor descriptor)
    {
        return new TaxOverview(
            true,
            message,
            "Нет данных",
            "BYN",
            0m,
            0m,
            0m,
            0m,
            0m,
            0m,
            0m,
            0m,
            13m,
            13m,
            0m,
            350_000m,
            0,
            "Курс: не требуется",
            "tax-draft-v1",
            descriptor.Name,
            descriptor.Description,
            false,
            string.Empty,
            [],
            [],
            "Расчет носит информационный характер и не является юридической консультацией.");
    }
}

public sealed record TaxBreakdownRow(string Name, decimal Value, string Note, TaxBreakdownKind Kind);

public sealed record TaxExportResult(bool Succeeded, string Message);

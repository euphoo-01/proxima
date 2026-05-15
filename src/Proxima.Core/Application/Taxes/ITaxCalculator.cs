namespace Proxima.Core.Application.Taxes;

public interface ITaxCalculator
{
    Task<TaxCalculationResult> CalculateAsync(
        IReadOnlyList<TaxTransactionSnapshot> transactions,
        int reportYear,
        LegalProfileType profile,
        string baseCurrency,
        CancellationToken cancellationToken = default);
}

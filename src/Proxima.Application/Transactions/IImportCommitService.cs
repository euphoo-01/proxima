using Proxima.Domain.Transactions;

namespace Proxima.Application.Transactions;

public interface IImportCommitService
{
    Task<ImportCommitResult> CommitAsync(Guid portfolioId, IReadOnlyList<ImportTransactionDraft> rows, CancellationToken cancellationToken = default);
}

public sealed record ImportTransactionDraft(
    DateTimeOffset TradeDate,
    string AssetTicker,
    TransactionType Type,
    decimal Quantity,
    decimal Price,
    decimal FeeAmount,
    string Currency,
    string? Notes);

public sealed record ImportCommitResult(bool Succeeded, int SavedRows, string Message);

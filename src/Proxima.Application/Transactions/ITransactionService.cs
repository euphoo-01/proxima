using Proxima.Domain.Transactions;

namespace Proxima.Application.Transactions;

public interface ITransactionService
{
    Task<IReadOnlyList<PortfolioTransaction>> ListActiveAsync(Guid portfolioId, CancellationToken cancellationToken = default);

    Task<TransactionOperationResult> CreateAsync(CreateTransactionRequest request, CancellationToken cancellationToken = default);

    Task<TransactionOperationResult> UpdateAsync(UpdateTransactionRequest request, CancellationToken cancellationToken = default);

    Task<TransactionOperationResult> ArchiveAsync(Guid portfolioId, Guid transactionId, CancellationToken cancellationToken = default);
}

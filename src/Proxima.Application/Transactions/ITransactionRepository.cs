using Proxima.Domain.Transactions;

namespace Proxima.Application.Transactions;

public interface ITransactionRepository
{
    Task<IReadOnlyList<PortfolioTransaction>> ListByPortfolioAsync(Guid portfolioId, bool includeArchived, CancellationToken cancellationToken);

    Task<PortfolioTransaction?> FindByIdAsync(Guid portfolioId, Guid transactionId, CancellationToken cancellationToken);

    Task AddAsync(PortfolioTransaction transaction, CancellationToken cancellationToken);

    Task UpdateAsync(PortfolioTransaction transaction, CancellationToken cancellationToken);
}

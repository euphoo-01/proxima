using Proxima.Core.Domain.Transactions;

namespace Proxima.Core.Application.Transactions;

public interface ITransactionRepository
{
    Task<IReadOnlyList<PortfolioTransaction>> ListByPortfolioAsync(Guid portfolioId, bool includeArchived, CancellationToken cancellationToken);

    Task<PortfolioTransaction?> FindByIdAsync(Guid portfolioId, Guid transactionId, CancellationToken cancellationToken);

    Task AddAsync(PortfolioTransaction transaction, CancellationToken cancellationToken);

    Task UpdateAsync(PortfolioTransaction transaction, CancellationToken cancellationToken);
}

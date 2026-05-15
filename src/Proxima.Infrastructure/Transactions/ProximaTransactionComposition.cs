using Proxima.Core.Application.Transactions;
using Proxima.Infrastructure.Persistence;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Transactions;

public static class ProximaTransactionComposition
{
    public static ITransactionService CreateTransactionService(
        IProximaUnitOfWorkFactory uowFactory,
        IProximaUnitOfWorkAccessor uowAccessor)
    {
        PostgresAssetRepository assetRepository = new(uowFactory, uowAccessor);
        return new TransactionService(new PostgresTransactionRepository(uowFactory, uowAccessor), assetRepository);
    }
}

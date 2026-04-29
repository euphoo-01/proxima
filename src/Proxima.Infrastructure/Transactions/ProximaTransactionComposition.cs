using Proxima.Application.Transactions;
using Proxima.Infrastructure.Assets;

namespace Proxima.Infrastructure.Transactions;

public static class ProximaTransactionComposition
{
    public static ITransactionService CreateTransactionService(string transactionStorePath, string assetStorePath)
    {
        return new TransactionService(new JsonTransactionRepository(transactionStorePath), new JsonAssetRepository(assetStorePath));
    }

    public static string GetDefaultTransactionStorePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Proxima", "transaction-store.json");
    }
}

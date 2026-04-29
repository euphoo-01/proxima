using Proxima.Domain.Transactions;

namespace Proxima.Application.Transactions;

public sealed record TransactionOperationResult(bool Succeeded, string Message, PortfolioTransaction? Transaction)
{
    public static TransactionOperationResult Success(PortfolioTransaction transaction)
    {
        return new TransactionOperationResult(true, string.Empty, transaction);
    }

    public static TransactionOperationResult Failure(string message)
    {
        return new TransactionOperationResult(false, message, null);
    }
}

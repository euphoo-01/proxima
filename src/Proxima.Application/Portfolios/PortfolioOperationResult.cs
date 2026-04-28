using Proxima.Domain.Portfolios;

namespace Proxima.Application.Portfolios;

public sealed record PortfolioOperationResult(
    bool Succeeded,
    string Message,
    Portfolio? Portfolio)
{
    public static PortfolioOperationResult Success(Portfolio portfolio, string message = "") =>
        new(true, message, portfolio);

    public static PortfolioOperationResult Failure(string message) =>
        new(false, message, null);
}

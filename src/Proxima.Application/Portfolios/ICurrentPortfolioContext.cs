namespace Proxima.Application.Portfolios;

public interface ICurrentPortfolioContext
{
    Guid CurrentPortfolioId { get; }

    string CurrentPortfolioName { get; }

    decimal CurrentPortfolioValue { get; }
}

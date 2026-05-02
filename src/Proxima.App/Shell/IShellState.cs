namespace Proxima.App.Shell;

public interface IShellState
{
    Guid CurrentPortfolioId { get; }

    string CurrentPortfolioName { get; }

    decimal CurrentPortfolioValue { get; }
}

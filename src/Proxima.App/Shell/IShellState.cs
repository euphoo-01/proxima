namespace Proxima.App.Shell;

public interface IShellState
{
    event EventHandler<ShellPortfolioChangedEventArgs>? PortfolioChanged;

    Guid CurrentPortfolioId { get; }

    string CurrentPortfolioName { get; }

    decimal CurrentPortfolioValue { get; }
}

public sealed record ShellPortfolioChangedEventArgs(
    Guid PortfolioId,
    string PortfolioName,
    decimal PortfolioValue);

public interface IShellPortfolioCoordinator
{
    void SetCurrentPortfolio(Guid portfolioId, string portfolioName, decimal portfolioValue);
}

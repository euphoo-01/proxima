namespace Proxima.App.Shell;

public sealed class RuntimeShellState : IShellState, IShellPortfolioCoordinator
{
    private readonly object _sync = new();
    private Guid _currentPortfolioId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private string _currentPortfolioName = "Demo Portfolio";
    private decimal _currentPortfolioValue = 18750m;

    public event EventHandler<ShellPortfolioChangedEventArgs>? PortfolioChanged;

    public Guid CurrentPortfolioId
    {
        get
        {
            lock (_sync)
            {
                return _currentPortfolioId;
            }
        }
    }

    public string CurrentPortfolioName
    {
        get
        {
            lock (_sync)
            {
                return _currentPortfolioName;
            }
        }
    }

    public decimal CurrentPortfolioValue
    {
        get
        {
            lock (_sync)
            {
                return _currentPortfolioValue;
            }
        }
    }

    public void SetCurrentPortfolio(Guid portfolioId, string portfolioName, decimal portfolioValue)
    {
        if (portfolioId == Guid.Empty)
        {
            return;
        }

        string normalizedName = string.IsNullOrWhiteSpace(portfolioName)
            ? "Portfolio"
            : portfolioName.Trim();

        bool changed;
        lock (_sync)
        {
            changed = _currentPortfolioId != portfolioId
                || !string.Equals(_currentPortfolioName, normalizedName, StringComparison.Ordinal)
                || _currentPortfolioValue != portfolioValue;

            _currentPortfolioId = portfolioId;
            _currentPortfolioName = normalizedName;
            _currentPortfolioValue = portfolioValue;
        }

        if (!changed)
        {
            return;
        }

        PortfolioChanged?.Invoke(this, new ShellPortfolioChangedEventArgs(portfolioId, normalizedName, portfolioValue));
    }
}

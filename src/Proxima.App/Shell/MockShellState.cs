namespace Proxima.App.Shell;

public sealed class MockShellState : IShellState
{
    public Guid CurrentPortfolioId { get; } = Guid.Parse("d0fd72f6-3ebc-44cc-b3bb-31f2f03fcaeb");

    public string CurrentPortfolioName => "Demo Portfolio";

    public decimal CurrentPortfolioValue => 18750m;
}

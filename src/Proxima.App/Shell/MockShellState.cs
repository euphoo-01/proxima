namespace Proxima.App.Shell;

public sealed class MockShellState : IShellState
{
    public Guid CurrentPortfolioId { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public string CurrentPortfolioName => "Demo Portfolio";

    public decimal CurrentPortfolioValue => 18750m;
}

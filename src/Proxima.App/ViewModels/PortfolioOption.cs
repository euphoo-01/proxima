namespace Proxima.App.ViewModels;

public sealed record PortfolioOption(
    Guid Id,
    string Name,
    string? Description,
    string? ClientLabel);

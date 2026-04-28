namespace Proxima.App.ViewModels;

public sealed record PortfolioOption(
    Guid Id,
    string Name,
    string Currency,
    string? Description,
    string? ClientLabel);

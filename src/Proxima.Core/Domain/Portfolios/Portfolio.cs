namespace Proxima.Core.Domain.Portfolios;

public sealed record Portfolio(
    Guid Id,
    Guid OwnerUserId,
    string Name,
    string? Description,
    string? ClientLabel,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

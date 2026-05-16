using Proxima.Core.Domain.Portfolios;

namespace Proxima.Core.Application.Portfolios;

public sealed class PortfolioService(IPortfolioRepository repository) : IPortfolioService
{
    public async Task<IReadOnlyList<Portfolio>> ListActiveAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Portfolio> portfolios = await repository.ListByOwnerAsync(ownerUserId, includeArchived: false, cancellationToken).ConfigureAwait(false);
        return portfolios.OrderBy(portfolio => portfolio.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<PortfolioOperationResult> CreateAsync(CreatePortfolioRequest request, CancellationToken cancellationToken = default)
    {
        string? validationError = Validate(request.Name);
        if (validationError is not null)
        {
            return PortfolioOperationResult.Failure(validationError);
        }

        IReadOnlyList<Portfolio> existing = await repository.ListByOwnerAsync(request.OwnerUserId, includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (existing.Any(portfolio => string.Equals(portfolio.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase) && !portfolio.IsArchived))
        {
            return PortfolioOperationResult.Failure("Портфель с таким именем уже существует.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Portfolio portfolio = new(
            Guid.NewGuid(),
            request.OwnerUserId,
            request.Name.Trim(),
            request.Description?.Trim(),
            request.ClientLabel?.Trim(),
            IsArchived: false,
            CreatedAt: now,
            UpdatedAt: now);

        await repository.AddAsync(portfolio, cancellationToken).ConfigureAwait(false);
        return PortfolioOperationResult.Success(portfolio, "Портфель создан.");
    }

    public async Task<PortfolioOperationResult> UpdateAsync(UpdatePortfolioRequest request, CancellationToken cancellationToken = default)
    {
        string? validationError = Validate(request.Name);
        if (validationError is not null)
        {
            return PortfolioOperationResult.Failure(validationError);
        }

        Portfolio? current = await repository.FindByIdAsync(request.OwnerUserId, request.PortfolioId, cancellationToken).ConfigureAwait(false);
        if (current is null || current.IsArchived)
        {
            return PortfolioOperationResult.Failure("Портфель не найден.");
        }

        IReadOnlyList<Portfolio> existing = await repository.ListByOwnerAsync(request.OwnerUserId, includeArchived: false, cancellationToken).ConfigureAwait(false);
        if (existing.Any(portfolio =>
                portfolio.Id != request.PortfolioId
                && string.Equals(portfolio.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return PortfolioOperationResult.Failure("Портфель с таким именем уже существует.");
        }

        Portfolio updated = current with
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            ClientLabel = request.ClientLabel?.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await repository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        return PortfolioOperationResult.Success(updated, "Портфель обновлён.");
    }

    public async Task<PortfolioOperationResult> ArchiveAsync(Guid ownerUserId, Guid portfolioId, CancellationToken cancellationToken = default)
    {
        Portfolio? current = await repository.FindByIdAsync(ownerUserId, portfolioId, cancellationToken).ConfigureAwait(false);
        if (current is null || current.IsArchived)
        {
            return PortfolioOperationResult.Failure("Портфель не найден.");
        }

        Portfolio archived = current with
        {
            IsArchived = true,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await repository.UpdateAsync(archived, cancellationToken).ConfigureAwait(false);
        return PortfolioOperationResult.Success(archived, "Портфель архивирован.");
    }

    private static string? Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Название портфеля обязательно.";
        }

        return null;
    }
}

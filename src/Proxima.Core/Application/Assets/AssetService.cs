using Proxima.Core.Domain.Assets;

namespace Proxima.Core.Application.Assets;

public sealed class AssetService(IAssetRepository repository) : IAssetService
{
    public Task<IReadOnlyList<Asset>> ListActiveAsync(Guid portfolioId, CancellationToken cancellationToken = default)
    {
        return repository.ListByPortfolioAsync(portfolioId, includeArchived: false, cancellationToken);
    }

    public async Task<AssetOperationResult> CreateAsync(CreateAssetRequest request, CancellationToken cancellationToken = default)
    {
        ValidationResult validation = Validate(request.PortfolioId, request.Ticker, request.Name, request.Currency, request.Quantity, request.AverageBuyPrice, request.CurrentPrice);
        if (!validation.IsValid)
        {
            return AssetOperationResult.Failure(validation.Message);
        }

        Asset asset = new(
            Guid.NewGuid(),
            request.PortfolioId,
            NormalizeTicker(request.Ticker),
            request.Name.Trim(),
            request.Type,
            request.Currency.Trim().ToUpperInvariant(),
            NormalizeOptional(request.Exchange),
            NormalizeOptional(request.Isin),
            NormalizeTags(request.Tags),
            request.Quantity,
            request.AverageBuyPrice,
            request.CurrentPrice,
            false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await repository.AddAsync(asset, cancellationToken).ConfigureAwait(false);
        return AssetOperationResult.Success(asset);
    }

    public async Task<AssetOperationResult> UpdateAsync(UpdateAssetRequest request, CancellationToken cancellationToken = default)
    {
        ValidationResult validation = Validate(request.PortfolioId, request.Ticker, request.Name, request.Currency, request.Quantity, request.AverageBuyPrice, request.CurrentPrice);
        if (!validation.IsValid)
        {
            return AssetOperationResult.Failure(validation.Message);
        }

        Asset? existing = await repository.FindByIdAsync(request.PortfolioId, request.AssetId, cancellationToken).ConfigureAwait(false);
        if (existing is null || existing.IsArchived)
        {
            return AssetOperationResult.Failure("Актив не найден.");
        }

        Asset updated = existing with
        {
            Ticker = NormalizeTicker(request.Ticker),
            Name = request.Name.Trim(),
            Type = request.Type,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Exchange = NormalizeOptional(request.Exchange),
            Isin = NormalizeOptional(request.Isin),
            Tags = NormalizeTags(request.Tags),
            Quantity = request.Quantity,
            AverageBuyPrice = request.AverageBuyPrice,
            CurrentPrice = request.CurrentPrice,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await repository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        return AssetOperationResult.Success(updated);
    }

    public async Task<AssetOperationResult> ArchiveAsync(Guid portfolioId, Guid assetId, CancellationToken cancellationToken = default)
    {
        Asset? existing = await repository.FindByIdAsync(portfolioId, assetId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return AssetOperationResult.Failure("Актив не найден.");
        }

        if (existing.IsArchived)
        {
            return AssetOperationResult.Failure("Актив уже архивирован.");
        }

        Asset archived = existing with
        {
            IsArchived = true,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await repository.UpdateAsync(archived, cancellationToken).ConfigureAwait(false);
        return AssetOperationResult.Success(archived);
    }

    private static ValidationResult Validate(Guid portfolioId, string ticker, string name, string currency, decimal quantity, decimal avgPrice, decimal currentPrice)
    {
        if (portfolioId == Guid.Empty)
        {
            return ValidationResult.Fail("Портфель не выбран.");
        }

        if (string.IsNullOrWhiteSpace(ticker))
        {
            return ValidationResult.Fail("Тикер обязателен.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return ValidationResult.Fail("Название актива обязательно.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return ValidationResult.Fail("Валюта обязательна.");
        }

        if (quantity < 0 || avgPrice < 0 || currentPrice < 0)
        {
            return ValidationResult.Fail("Количество и цены должны быть неотрицательными.");
        }

        return ValidationResult.Ok();
    }

    private static string NormalizeTicker(string ticker)
    {
        return ticker.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return [];
        }

        return tags
            .Select(static item => item.Trim())
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private readonly record struct ValidationResult(bool IsValid, string Message)
    {
        public static ValidationResult Ok()
        {
            return new ValidationResult(true, string.Empty);
        }

        public static ValidationResult Fail(string message)
        {
            return new ValidationResult(false, message);
        }
    }
}

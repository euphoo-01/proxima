using Proxima.Application.Assets;
using Proxima.Domain.Transactions;

namespace Proxima.Application.Transactions;

public sealed class TransactionService(ITransactionRepository repository, IAssetRepository assets) : ITransactionService
{
    public Task<IReadOnlyList<PortfolioTransaction>> ListActiveAsync(Guid portfolioId, CancellationToken cancellationToken = default)
    {
        return repository.ListByPortfolioAsync(portfolioId, includeArchived: false, cancellationToken);
    }

    public async Task<TransactionOperationResult> CreateAsync(CreateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await ValidateAsync(
            request.PortfolioId,
            request.AssetId,
            request.Type,
            request.TradeDate,
            request.Quantity,
            request.Price,
            request.GrossAmount,
            request.Currency,
            cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return TransactionOperationResult.Failure(validation.Message);
        }

        PortfolioTransaction transaction = new(
            Guid.NewGuid(),
            request.PortfolioId,
            request.AssetId,
            request.Type,
            request.TradeDate,
            request.Quantity,
            request.Price,
            request.GrossAmount,
            request.FeeAmount,
            request.TaxAmount,
            request.Currency.Trim().ToUpperInvariant(),
            NormalizeOptional(request.Broker),
            NormalizeOptional(request.ExternalId),
            ObfuscateNotes(request.Notes),
            false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await repository.AddAsync(transaction, cancellationToken).ConfigureAwait(false);
        return TransactionOperationResult.Success(transaction);
    }

    public async Task<TransactionOperationResult> UpdateAsync(UpdateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await ValidateAsync(
            request.PortfolioId,
            request.AssetId,
            request.Type,
            request.TradeDate,
            request.Quantity,
            request.Price,
            request.GrossAmount,
            request.Currency,
            cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return TransactionOperationResult.Failure(validation.Message);
        }

        PortfolioTransaction? existing = await repository.FindByIdAsync(request.PortfolioId, request.TransactionId, cancellationToken).ConfigureAwait(false);
        if (existing is null || existing.IsArchived)
        {
            return TransactionOperationResult.Failure("Транзакция не найдена.");
        }

        PortfolioTransaction updated = existing with
        {
            AssetId = request.AssetId,
            Type = request.Type,
            TradeDate = request.TradeDate,
            Quantity = request.Quantity,
            Price = request.Price,
            GrossAmount = request.GrossAmount,
            FeeAmount = request.FeeAmount,
            TaxAmount = request.TaxAmount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Broker = NormalizeOptional(request.Broker),
            ExternalId = NormalizeOptional(request.ExternalId),
            EncryptedNotes = ObfuscateNotes(request.Notes),
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await repository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        return TransactionOperationResult.Success(updated);
    }

    public async Task<TransactionOperationResult> ArchiveAsync(Guid portfolioId, Guid transactionId, CancellationToken cancellationToken = default)
    {
        PortfolioTransaction? existing = await repository.FindByIdAsync(portfolioId, transactionId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return TransactionOperationResult.Failure("Транзакция не найдена.");
        }

        if (existing.IsArchived)
        {
            return TransactionOperationResult.Failure("Транзакция уже архивирована.");
        }

        PortfolioTransaction archived = existing with
        {
            IsArchived = true,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await repository.UpdateAsync(archived, cancellationToken).ConfigureAwait(false);
        return TransactionOperationResult.Success(archived);
    }

    private async Task<ValidationResult> ValidateAsync(
        Guid portfolioId,
        Guid? assetId,
        TransactionType type,
        DateTimeOffset tradeDate,
        decimal quantity,
        decimal price,
        decimal grossAmount,
        string currency,
        CancellationToken cancellationToken)
    {
        if (portfolioId == Guid.Empty)
        {
            return ValidationResult.Fail("Портфель обязателен.");
        }

        if (tradeDate == default)
        {
            return ValidationResult.Fail("Дата операции обязательна.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return ValidationResult.Fail("Валюта обязательна.");
        }

        if (quantity < 0 || price < 0 || grossAmount < 0)
        {
            return ValidationResult.Fail("Количество и суммы должны быть неотрицательными.");
        }

        if (RequiresAsset(type))
        {
            if (assetId is null)
            {
                return ValidationResult.Fail("Для этого типа операции нужно выбрать актив.");
            }

            if (await assets.FindByIdAsync(portfolioId, assetId.Value, cancellationToken).ConfigureAwait(false) is null)
            {
                return ValidationResult.Fail("Актив не найден в выбранном портфеле.");
            }
        }

        if ((type is TransactionType.Buy or TransactionType.Sell) && (quantity <= 0 || price <= 0))
        {
            return ValidationResult.Fail("Для Buy/Sell укажите количество и цену больше нуля.");
        }

        if ((type is TransactionType.Dividend or TransactionType.Fee or TransactionType.Tax) && grossAmount <= 0)
        {
            return ValidationResult.Fail("Для выбранного типа сумма операции должна быть больше нуля.");
        }

        return ValidationResult.Ok();
    }

    private static bool RequiresAsset(TransactionType type)
    {
        return type is TransactionType.Buy
            or TransactionType.Sell
            or TransactionType.Dividend
            or TransactionType.Split
            or TransactionType.Airdrop
            or TransactionType.StakingReward;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string? ObfuscateNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(notes.Trim()));
    }

    private readonly record struct ValidationResult(bool IsValid, string Message)
    {
        public static ValidationResult Ok() => new(true, string.Empty);
        public static ValidationResult Fail(string message) => new(false, message);
    }
}

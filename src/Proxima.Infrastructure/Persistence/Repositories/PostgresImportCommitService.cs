using Microsoft.EntityFrameworkCore;
using Proxima.Application.Transactions;

namespace Proxima.Infrastructure.Persistence.Repositories;

public sealed class PostgresImportCommitService(IProximaUnitOfWorkFactory uowFactory) : IImportCommitService
{
    public async Task<ImportCommitResult> CommitAsync(Guid portfolioId, IReadOnlyList<ImportTransactionDraft> rows, CancellationToken cancellationToken = default)
    {
        if (portfolioId == Guid.Empty)
        {
            return new ImportCommitResult(false, 0, "Портфель обязателен.");
        }

        if (rows.Count == 0)
        {
            return new ImportCommitResult(true, 0, "Нет строк для импорта.");
        }

        try
        {
            return await uowFactory.ExecuteInTransactionAsync(async uow =>
            {
                ProximaDbContext ctx = uow.Context;
                Dictionary<string, Guid> assetByTicker = await ctx.Assets.AsNoTracking()
                    .Where(x => x.PortfolioId == portfolioId)
                    .ToDictionaryAsync(x => x.Ticker, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken)
                    .ConfigureAwait(false);

                DateTimeOffset now = DateTimeOffset.UtcNow;
                foreach (ImportTransactionDraft row in rows)
                {
                    Guid? assetId = assetByTicker.TryGetValue(row.AssetTicker.Trim(), out Guid id) ? id : null;
                    decimal gross = row.Quantity * row.Price;
                    ctx.Transactions.Add(new TransactionEntity
                    {
                        Id = Guid.NewGuid(),
                        PortfolioId = portfolioId,
                        AssetId = assetId,
                        Type = row.Type.ToString(),
                        TradeDate = row.TradeDate,
                        Quantity = row.Quantity,
                        Price = row.Price,
                        GrossAmount = gross,
                        FeeAmount = row.FeeAmount,
                        TaxAmount = 0m,
                        Currency = row.Currency.Trim().ToUpperInvariant(),
                        EncryptedNotes = row.Notes,
                        IsArchived = false,
                        CreatedAt = now,
                        UpdatedAt = now,
                    });
                }

                return new ImportCommitResult(true, rows.Count, $"Импортировано строк: {rows.Count}");
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new ImportCommitResult(false, 0, $"Импорт отменён: {ex.Message}");
        }
    }
}

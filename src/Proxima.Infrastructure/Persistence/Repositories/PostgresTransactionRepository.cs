using Microsoft.EntityFrameworkCore;
using Proxima.Core.Application.Transactions;
using Proxima.Core.Domain.Transactions;

namespace Proxima.Infrastructure.Persistence.Repositories;

public sealed class PostgresTransactionRepository(IProximaUnitOfWorkFactory uowFactory, IProximaUnitOfWorkAccessor uowAccessor) : ITransactionRepository
{
    public async Task<IReadOnlyList<PortfolioTransaction>> ListByPortfolioAsync(Guid portfolioId, bool includeArchived, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        IQueryable<TransactionEntity> query = ctx.Transactions.AsNoTracking().Where(x => x.PortfolioId == portfolioId);
        if (!includeArchived)
        {
            query = query.Where(x => !x.IsArchived);
        }

        return await query.OrderByDescending(x => x.TradeDate)
            .Select(x => new PortfolioTransaction(
                x.Id,
                x.PortfolioId,
                x.AssetId,
                Enum.Parse<TransactionType>(x.Type, true),
                x.TradeDate,
                x.Quantity,
                x.Price,
                x.GrossAmount,
                x.FeeAmount,
                x.TaxAmount,
                x.Currency,
                x.Broker,
                x.ExternalId,
                x.EncryptedNotes,
                x.IsArchived,
                x.CreatedAt,
                x.UpdatedAt))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PortfolioTransaction?> FindByIdAsync(Guid portfolioId, Guid transactionId, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        TransactionEntity? x = await ctx.Transactions.AsNoTracking()
            .FirstOrDefaultAsync(i => i.PortfolioId == portfolioId && i.Id == transactionId, cancellationToken)
            .ConfigureAwait(false);

        return x is null
            ? null
            : new PortfolioTransaction(
                x.Id,
                x.PortfolioId,
                x.AssetId,
                Enum.Parse<TransactionType>(x.Type, true),
                x.TradeDate,
                x.Quantity,
                x.Price,
                x.GrossAmount,
                x.FeeAmount,
                x.TaxAmount,
                x.Currency,
                x.Broker,
                x.ExternalId,
                x.EncryptedNotes,
                x.IsArchived,
                x.CreatedAt,
                x.UpdatedAt);
    }

    public async Task AddAsync(PortfolioTransaction transaction, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        ctx.Transactions.Add(new TransactionEntity
        {
            Id = transaction.Id,
            PortfolioId = transaction.PortfolioId,
            AssetId = transaction.AssetId,
            Type = transaction.Type.ToString(),
            TradeDate = NormalizeTradeDateForPostgres(transaction.TradeDate),
            Quantity = transaction.Quantity,
            Price = transaction.Price,
            GrossAmount = transaction.GrossAmount,
            FeeAmount = transaction.FeeAmount,
            TaxAmount = transaction.TaxAmount,
            Currency = transaction.Currency,
            Broker = transaction.Broker,
            ExternalId = transaction.ExternalId,
            EncryptedNotes = transaction.EncryptedNotes,
            IsArchived = transaction.IsArchived,
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt,
        });
        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(PortfolioTransaction transaction, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        TransactionEntity entity = await ctx.Transactions.FirstAsync(
            x => x.PortfolioId == transaction.PortfolioId && x.Id == transaction.Id,
            cancellationToken).ConfigureAwait(false);

        entity.AssetId = transaction.AssetId;
        entity.Type = transaction.Type.ToString();
        entity.TradeDate = NormalizeTradeDateForPostgres(transaction.TradeDate);
        entity.Quantity = transaction.Quantity;
        entity.Price = transaction.Price;
        entity.GrossAmount = transaction.GrossAmount;
        entity.FeeAmount = transaction.FeeAmount;
        entity.TaxAmount = transaction.TaxAmount;
        entity.Currency = transaction.Currency;
        entity.Broker = transaction.Broker;
        entity.ExternalId = transaction.ExternalId;
        entity.EncryptedNotes = transaction.EncryptedNotes;
        entity.IsArchived = transaction.IsArchived;
        entity.UpdatedAt = transaction.UpdatedAt;

        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DateTimeOffset NormalizeTradeDateForPostgres(DateTimeOffset value)
    {
        if (value.TimeOfDay == TimeSpan.Zero)
        {
            return new DateTimeOffset(value.Year, value.Month, value.Day, 12, 0, 0, TimeSpan.Zero);
        }

        return value.ToUniversalTime();
    }
}

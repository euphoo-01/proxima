using System.Threading;
using Microsoft.EntityFrameworkCore.Storage;

namespace Proxima.Infrastructure.Persistence;

public sealed class ProximaUnitOfWork(DatabaseBootstrapService db) : IProximaUnitOfWork
{
    private int _disposed;

    public ProximaDbContext Context { get; } = db.CreateDbContext();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Context.SaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Context.Database.BeginTransactionAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        Context.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await Context.DisposeAsync().ConfigureAwait(false);
    }
}

public sealed class ProximaUnitOfWorkAccessor : IProximaUnitOfWorkAccessor
{
    private static readonly AsyncLocal<IProximaUnitOfWork?> Slot = new();

    public IProximaUnitOfWork? Current
    {
        get => Slot.Value;
        set => Slot.Value = value;
    }
}

public sealed class ProximaUnitOfWorkFactory(DatabaseBootstrapService db, IProximaUnitOfWorkAccessor accessor) : IProximaUnitOfWorkFactory
{
    public IProximaUnitOfWork Create()
    {
        return new ProximaUnitOfWork(db);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<IProximaUnitOfWork, Task<T>> action, CancellationToken cancellationToken = default)
    {
        await using IProximaUnitOfWork uow = Create();
        IProximaUnitOfWork? previous = accessor.Current;
        accessor.Current = uow;

        try
        {
            await using IDbContextTransaction tx = await uow.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            T result = await action(uow).ConfigureAwait(false);
            await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        finally
        {
            accessor.Current = previous;
        }
    }

    public async Task ExecuteInTransactionAsync(Func<IProximaUnitOfWork, Task> action, CancellationToken cancellationToken = default)
    {
        await ExecuteInTransactionAsync(async uow =>
        {
            await action(uow).ConfigureAwait(false);
            return 0;
        }, cancellationToken).ConfigureAwait(false);
    }
}

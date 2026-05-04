namespace Proxima.Infrastructure.Persistence.Repositories;

internal readonly struct UowLease : IAsyncDisposable
{
    private readonly IProximaUnitOfWork? _owned;

    private UowLease(ProximaDbContext context, IProximaUnitOfWork? owned)
    {
        Context = context;
        _owned = owned;
    }

    public ProximaDbContext Context { get; }

    public static UowLease Create(IProximaUnitOfWorkFactory factory, IProximaUnitOfWorkAccessor accessor)
    {
        IProximaUnitOfWork? current = accessor.Current;
        if (current is not null)
        {
            return new UowLease(current.Context, null);
        }

        IProximaUnitOfWork created = factory.Create();
        return new UowLease(created.Context, created);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _owned is null ? Task.FromResult(0) : _owned.SaveChangesAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _owned is null ? ValueTask.CompletedTask : _owned.DisposeAsync();
    }
}

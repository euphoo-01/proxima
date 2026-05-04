using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Proxima.Infrastructure.Persistence;

public interface IProximaUnitOfWork : IAsyncDisposable, IDisposable
{
    ProximaDbContext Context { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public interface IProximaUnitOfWorkAccessor
{
    IProximaUnitOfWork? Current { get; set; }
}

public interface IProximaUnitOfWorkFactory
{
    IProximaUnitOfWork Create();

    Task<T> ExecuteInTransactionAsync<T>(Func<IProximaUnitOfWork, Task<T>> action, CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(Func<IProximaUnitOfWork, Task> action, CancellationToken cancellationToken = default);
}

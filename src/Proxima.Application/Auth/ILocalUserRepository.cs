using Proxima.Domain.Auth;

namespace Proxima.Application.Auth;

public interface ILocalUserRepository
{
    Task<bool> HasAnyProfileAsync(CancellationToken cancellationToken);

    Task<LocalUserProfile?> FindByLoginAsync(string login, CancellationToken cancellationToken);

    Task AddAsync(LocalUserProfile profile, CancellationToken cancellationToken);

    Task UpdateAsync(LocalUserProfile profile, CancellationToken cancellationToken);
}

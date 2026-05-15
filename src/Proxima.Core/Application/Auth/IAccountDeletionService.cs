namespace Proxima.Core.Application.Auth;

public interface IAccountDeletionService
{
    Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken = default);
}

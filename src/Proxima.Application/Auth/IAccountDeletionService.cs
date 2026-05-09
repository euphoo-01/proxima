namespace Proxima.Application.Auth;

public interface IAccountDeletionService
{
    Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken = default);
}

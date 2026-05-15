namespace Proxima.Core.Application.Auth;

public interface ILocalAuthService
{
    Task<bool> NeedsFirstRunSetupAsync(CancellationToken cancellationToken = default);

    Task<AuthResult> CreateProfileAsync(CreateProfileRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> UnlockAsync(string login, string password, CancellationToken cancellationToken = default);

    Task DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken = default);
}

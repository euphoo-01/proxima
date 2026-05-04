using Proxima.Application.Auth;
using Proxima.Domain.Auth;

namespace Proxima.App.Views.Auth;

public interface IRuntimeUserContext
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    string Login { get; }

    string DisplayName { get; }

    UserRole Role { get; }

    void SetAuthenticated(LocalUserProfile profile);
}

public sealed class RuntimeUserContext : IRuntimeUserContext
{
    public bool IsAuthenticated { get; private set; }

    public Guid UserId { get; private set; }

    public string Login { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public UserRole Role { get; private set; } = UserRole.PrivateInvestor;

    public void SetAuthenticated(LocalUserProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        IsAuthenticated = true;
        UserId = profile.Id;
        Login = profile.Login;
        DisplayName = profile.DisplayName;
        Role = profile.Role;
    }
}

public interface IRuntimeAuthBootstrapper
{
    Task<LocalUserProfile> EnsureRuntimeProfileAsync(CancellationToken cancellationToken = default);
}

public sealed class RuntimeAuthBootstrapper(ILocalAuthService authService) : IRuntimeAuthBootstrapper
{
    public async Task<LocalUserProfile> EnsureRuntimeProfileAsync(CancellationToken cancellationToken = default)
    {
        if (!await authService.NeedsFirstRunSetupAsync(cancellationToken).ConfigureAwait(false))
        {
            AuthResult unlock = await authService.UnlockAsync("local", "Proxima123!", cancellationToken).ConfigureAwait(false);
            if (unlock.Succeeded && unlock.Profile is not null)
            {
                return unlock.Profile;
            }

            throw new InvalidOperationException("Runtime profile exists but cannot be unlocked with bootstrap credentials.");
        }

        AuthResult created = await authService.CreateProfileAsync(
            new CreateProfileRequest(
                "Local User",
                "local",
                "Proxima123!",
                "Proxima123!",
                UserRole.PrivateInvestor),
            cancellationToken).ConfigureAwait(false);

        if (!created.Succeeded || created.Profile is null)
        {
            throw new InvalidOperationException(created.UserMessage);
        }

        return created.Profile;
    }
}

public sealed class LocalProfileAuthGateService(
    ILocalAuthService localAuthService,
    IRuntimeUserContext runtimeUserContext)
    : IAuthGateService
{
    public async Task<AuthGateResult> UnlockAsync(string loginOrEmail, string password, CancellationToken cancellationToken = default)
    {
        AuthResult result = await localAuthService.UnlockAsync(loginOrEmail, password, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Profile is null)
        {
            return AuthGateResult.Fail(string.IsNullOrWhiteSpace(result.UserMessage) ? "Invalid credentials." : result.UserMessage);
        }

        runtimeUserContext.SetAuthenticated(result.Profile);
        return AuthGateResult.Success();
    }
}

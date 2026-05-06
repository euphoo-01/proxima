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

public sealed class RuntimeUserContext : IRuntimeUserContext, ICurrentUserContext
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
    Task<RuntimeAuthBootstrapResult> EnsureRuntimeProfileAsync(CancellationToken cancellationToken = default);

    Task<AuthResult> TryDevAutoLoginAsync(CancellationToken cancellationToken = default);
}

public sealed class RuntimeAuthBootstrapper(ILocalAuthService authService) : IRuntimeAuthBootstrapper
{
    public async Task<RuntimeAuthBootstrapResult> EnsureRuntimeProfileAsync(CancellationToken cancellationToken = default)
    {
        bool firstRunRequired = await authService
            .NeedsFirstRunSetupAsync(cancellationToken)
            .ConfigureAwait(false);

        if (firstRunRequired)
        {
            return RuntimeAuthBootstrapResult.FirstRunRequired();
        }

        return RuntimeAuthBootstrapResult.ReadyForUnlock();
    }

    public async Task<AuthResult> TryDevAutoLoginAsync(CancellationToken cancellationToken = default)
    {
        string? login = Environment.GetEnvironmentVariable("PROXIMA_DEV_AUTOLOGIN_LOGIN");
        string? password = Environment.GetEnvironmentVariable("PROXIMA_DEV_AUTOLOGIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            return AuthResult.Failure(
                AuthFailureReason.GenericAuthenticationFailed,
                "Dev auto-login requires PROXIMA_DEV_AUTOLOGIN_LOGIN and PROXIMA_DEV_AUTOLOGIN_PASSWORD.");
        }

        return await authService
            .UnlockAsync(login, password, cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed record RuntimeAuthBootstrapResult(bool IsFirstRunRequired)
{
    public static RuntimeAuthBootstrapResult ReadyForUnlock() => new(false);

    public static RuntimeAuthBootstrapResult FirstRunRequired() => new(true);
}

public sealed class LocalProfileAuthGateService(
    ILocalAuthService localAuthService,
    IRuntimeUserContext runtimeUserContext)
    : IAuthGateService
{
    public async Task<AuthGateResult> UnlockAsync(
        string loginOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        AuthResult result = await localAuthService
            .UnlockAsync(loginOrEmail, password, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded || result.Profile is null)
        {
            return AuthGateResult.Fail(
                string.IsNullOrWhiteSpace(result.UserMessage)
                    ? "Invalid credentials."
                    : result.UserMessage);
        }

        runtimeUserContext.SetAuthenticated(result.Profile);
        return AuthGateResult.Success();
    }
}

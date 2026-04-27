using Proxima.Domain.Auth;

namespace Proxima.Application.Auth;

public sealed record AuthResult(
    bool Succeeded,
    LocalUserProfile? Profile,
    AuthFailureReason FailureReason,
    string UserMessage)
{
    public static AuthResult Success(LocalUserProfile profile) =>
        new(true, profile, AuthFailureReason.None, string.Empty);

    public static AuthResult Failure(AuthFailureReason reason, string userMessage) =>
        new(false, null, reason, userMessage);
}

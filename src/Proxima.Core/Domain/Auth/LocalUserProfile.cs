namespace Proxima.Core.Domain.Auth;

public sealed record LocalUserProfile(
    Guid Id,
    string DisplayName,
    string Login,
    UserRole Role,
    PasswordCredential Credential,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int FailedUnlockAttempts);

namespace Proxima.Core.Domain.Auth;

/// <summary>
/// Self-contained encoded password credential.
///
/// The value contains the password hashing algorithm, format version,
/// work factor, salt and derived hash. It is safe to persist as a single
/// database column, but it must never contain the plaintext password.
/// </summary>
public sealed record PasswordCredential(string EncodedHash)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(EncodedHash);
}

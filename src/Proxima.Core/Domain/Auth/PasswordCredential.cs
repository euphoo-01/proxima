namespace Proxima.Core.Domain.Auth;

public sealed record PasswordCredential(string EncodedHash)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(EncodedHash);
}

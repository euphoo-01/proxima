namespace Proxima.Domain.Auth;

public sealed record PasswordCredential(
    string Algorithm,
    byte[] Salt,
    byte[] Hash,
    int Iterations,
    int Version);

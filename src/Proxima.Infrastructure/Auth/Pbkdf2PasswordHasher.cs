using System.Security.Cryptography;
using Proxima.Application.Auth;
using Proxima.Domain.Auth;

namespace Proxima.Infrastructure.Auth;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const string AlgorithmName = "PBKDF2-SHA256";
    private const int SaltSize = 32;
    private const int HashSize = 32;
    private const int DefaultIterations = 210_000;
    private const int Version = 1;

    public PasswordCredential Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, DefaultIterations, HashAlgorithmName.SHA256, HashSize);

        return new PasswordCredential(AlgorithmName, salt, hash, DefaultIterations, Version);
    }

    public bool Verify(string password, PasswordCredential credential)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(credential);

        if (!string.Equals(credential.Algorithm, AlgorithmName, StringComparison.Ordinal)
            || credential.Salt.Length == 0
            || credential.Hash.Length == 0
            || credential.Iterations <= 0)
        {
            return false;
        }

        byte[] candidate = Rfc2898DeriveBytes.Pbkdf2(
            password,
            credential.Salt,
            credential.Iterations,
            HashAlgorithmName.SHA256,
            credential.Hash.Length);

        return CryptographicOperations.FixedTimeEquals(candidate, credential.Hash);
    }
}

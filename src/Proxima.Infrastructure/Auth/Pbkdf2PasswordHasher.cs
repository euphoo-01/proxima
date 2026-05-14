using System.Globalization;
using System.Security.Cryptography;
using Proxima.Application.Auth;
using Proxima.Domain.Auth;

namespace Proxima.Infrastructure.Auth;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const string AlgorithmName = "pbkdf2-sha256";
    private const int SaltSize = 32;
    private const int HashSize = 32;
    private const int DefaultIterations = 210_000;
    private const int Version = 1;
    private const char SegmentSeparator = '$';

    public PasswordCredential Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, DefaultIterations, HashAlgorithmName.SHA256, HashSize);

        string encoded = string.Join(
            SegmentSeparator,
            string.Empty,
            AlgorithmName,
            $"v={Version}",
            $"i={DefaultIterations}",
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));

        return new PasswordCredential(encoded);
    }

    public bool Verify(string password, PasswordCredential credential)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(credential);

        if (!TryDecode(credential.EncodedHash, out DecodedCredential decoded))
        {
            return false;
        }

        byte[] candidate = Rfc2898DeriveBytes.Pbkdf2(
            password,
            decoded.Salt,
            decoded.Iterations,
            HashAlgorithmName.SHA256,
            decoded.Hash.Length);

        return CryptographicOperations.FixedTimeEquals(candidate, decoded.Hash);
    }

    private static bool TryDecode(string encodedHash, out DecodedCredential credential)
    {
        credential = default;

        if (string.IsNullOrWhiteSpace(encodedHash))
        {
            return false;
        }

        string[] parts = encodedHash.Split(SegmentSeparator, StringSplitOptions.None);
        if (parts.Length != 6
            || parts[0].Length != 0
            || !string.Equals(parts[1], AlgorithmName, StringComparison.Ordinal)
            || !TryReadPrefixedInt(parts[2], "v=", out int version)
            || version != Version
            || !TryReadPrefixedInt(parts[3], "i=", out int iterations)
            || iterations <= 0)
        {
            return false;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(parts[4]);
            byte[] hash = Convert.FromBase64String(parts[5]);

            if (salt.Length == 0 || hash.Length == 0)
            {
                return false;
            }

            credential = new DecodedCredential(iterations, salt, hash);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryReadPrefixedInt(string value, string prefix, out int result)
    {
        result = 0;
        return value.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(value[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out result);
    }

    private readonly record struct DecodedCredential(int Iterations, byte[] Salt, byte[] Hash);
}

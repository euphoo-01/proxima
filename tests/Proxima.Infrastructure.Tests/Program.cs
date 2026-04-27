using Proxima.Infrastructure;
using Proxima.Application.Auth;
using Proxima.Domain.Auth;
using Proxima.Infrastructure.Auth;

namespace Proxima.Infrastructure.Tests;

internal static class Program
{
    private static async Task Main()
    {
        string? assemblyName = typeof(InfrastructureAssemblyMarker).Assembly.GetName().Name;
        Assert(assemblyName == "Proxima.Infrastructure", "Infrastructure assembly name must be Proxima.Infrastructure.");
        Pbkdf2Hasher_VerifiesPasswordAndUsesUniqueSalt();
        await JsonRepository_PersistsProfileWithoutPlaintextPassword().ConfigureAwait(false);
        Console.WriteLine("Proxima.Infrastructure.Tests passed.");
    }

    private static void Pbkdf2Hasher_VerifiesPasswordAndUsesUniqueSalt()
    {
        Pbkdf2PasswordHasher hasher = new();

        PasswordCredential first = hasher.Hash("Proxima2026");
        PasswordCredential second = hasher.Hash("Proxima2026");

        Assert(first.Hash.Length > 0, "Password hash must not be empty.");
        Assert(first.Salt.Length > 0, "Password salt must not be empty.");
        Assert(!first.Salt.SequenceEqual(second.Salt), "Same password must use different salts.");
        Assert(!first.Hash.SequenceEqual(second.Hash), "Same password with different salts must produce different hashes.");
        Assert(hasher.Verify("Proxima2026", first), "Correct password must verify.");
        Assert(!hasher.Verify("WrongPassword2026", first), "Incorrect password must fail.");
    }

    private static async Task JsonRepository_PersistsProfileWithoutPlaintextPassword()
    {
        string directory = Path.Combine(Path.GetTempPath(), "proxima-auth-tests", Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(directory, "profiles.json");
        JsonLocalUserRepository repository = new(filePath);
        LocalAuthService service = new(repository, new Pbkdf2PasswordHasher(), new PasswordPolicyValidator());

        AuthResult created = await service.CreateProfileAsync(new CreateProfileRequest(
            "Demo Investor",
            "demo",
            "Proxima2026!",
            "Proxima2026!",
            UserRole.PrivateInvestor)).ConfigureAwait(false);

        Assert(created.Succeeded, "Profile setup must persist through repository.");
        Assert(File.Exists(filePath), "Profile store file must exist.");

        string persistedJson = File.ReadAllText(filePath);
        Assert(!persistedJson.Contains("Proxima2026", StringComparison.Ordinal), "Profile store must not contain plaintext password.");
        Assert(!persistedJson.Contains("Proxima2026!", StringComparison.Ordinal), "Profile store must not contain plaintext password with symbols.");
        Assert(persistedJson.Contains(Pbkdf2PasswordHasher.AlgorithmName, StringComparison.Ordinal), "Profile store must record password algorithm metadata.");

        JsonLocalUserRepository reloadedRepository = new(filePath);
        LocalUserProfile? reloaded = await reloadedRepository.FindByLoginAsync("demo", CancellationToken.None).ConfigureAwait(false);
        Assert(reloaded is not null, "Persisted profile must reload by login.");

        AuthResult unlocked = await new LocalAuthService(reloadedRepository, new Pbkdf2PasswordHasher(), new PasswordPolicyValidator())
            .UnlockAsync("demo", "Proxima2026!")
            .ConfigureAwait(false);
        Assert(unlocked.Succeeded, "Reloaded profile must unlock with correct password.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

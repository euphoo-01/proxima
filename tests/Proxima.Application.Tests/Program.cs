using Proxima.Application;
using Proxima.Application.Auth;
using Proxima.Domain.Auth;

namespace Proxima.Application.Tests;

internal static class Program
{
    private static async Task Main()
    {
        string? assemblyName = typeof(ApplicationAssemblyMarker).Assembly.GetName().Name;
        Assert(assemblyName == "Proxima.Application", "Application assembly name must be Proxima.Application.");
        PasswordValidator_RejectsWeakPasswords();
        PasswordValidator_AcceptsStrongPassword();
        await AuthService_CreatesProfileAndUnlocks().ConfigureAwait(false);
        await AuthService_ReturnsGenericErrorForUnknownLoginAndWrongPassword().ConfigureAwait(false);
        Console.WriteLine("Proxima.Application.Tests passed.");
    }

    private static void PasswordValidator_RejectsWeakPasswords()
    {
        PasswordPolicyValidator validator = new();

        Assert(!validator.Validate("short1", "short1").IsValid, "Short password must be rejected.");
        Assert(!validator.Validate("password", "password").IsValid, "Password without digit or symbol must be rejected.");
        Assert(!validator.Validate("password1", "different1").IsValid, "Mismatched confirmation must be rejected.");
    }

    private static void PasswordValidator_AcceptsStrongPassword()
    {
        PasswordValidationResult result = new PasswordPolicyValidator().Validate("Proxima2026", "Proxima2026");
        Assert(result.IsValid, "Strong matching password must be accepted.");
    }

    private static async Task AuthService_CreatesProfileAndUnlocks()
    {
        MemoryUserRepository repository = new();
        LocalAuthService service = new(repository, new TestPasswordHasher(), new PasswordPolicyValidator());

        Assert(await service.NeedsFirstRunSetupAsync().ConfigureAwait(false), "Empty repository must require first-run setup.");

        AuthResult created = await service.CreateProfileAsync(new CreateProfileRequest(
            "Investor",
            "Demo",
            "Proxima2026",
            "Proxima2026",
            UserRole.PrivateInvestor)).ConfigureAwait(false);

        Assert(created.Succeeded, "Valid profile setup must succeed.");
        Assert(!await service.NeedsFirstRunSetupAsync().ConfigureAwait(false), "Created profile must disable first-run setup.");

        AuthResult unlocked = await service.UnlockAsync("demo", "Proxima2026").ConfigureAwait(false);
        Assert(unlocked.Succeeded, "Correct password must unlock.");
        Assert(repository.UpdatedProfiles.Count > 0, "Successful unlock must update profile state.");
    }

    private static async Task AuthService_ReturnsGenericErrorForUnknownLoginAndWrongPassword()
    {
        MemoryUserRepository repository = new();
        LocalAuthService service = new(repository, new TestPasswordHasher(), new PasswordPolicyValidator());

        await service.CreateProfileAsync(new CreateProfileRequest(
            "Investor",
            "demo",
            "Proxima2026",
            "Proxima2026",
            UserRole.PrivateInvestor)).ConfigureAwait(false);

        AuthResult unknownLogin = await service.UnlockAsync("missing", "Proxima2026").ConfigureAwait(false);
        AuthResult wrongPassword = await service.UnlockAsync("demo", "wrong-password").ConfigureAwait(false);

        Assert(!unknownLogin.Succeeded, "Unknown login must fail.");
        Assert(!wrongPassword.Succeeded, "Wrong password must fail.");
        Assert(
            unknownLogin.FailureReason == AuthFailureReason.GenericAuthenticationFailed
            && wrongPassword.FailureReason == AuthFailureReason.GenericAuthenticationFailed,
            "Unknown login and wrong password must use the same failure reason.");
        Assert(unknownLogin.UserMessage == wrongPassword.UserMessage, "Unknown login and wrong password must show the same message.");
        Assert(repository.Profiles.Single().FailedUnlockAttempts == 1, "Wrong password must increment failed attempts.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class MemoryUserRepository : ILocalUserRepository
    {
        private readonly List<LocalUserProfile> _profiles = [];

        public IReadOnlyList<LocalUserProfile> Profiles => _profiles;

        public List<LocalUserProfile> UpdatedProfiles { get; } = [];

        public Task<bool> HasAnyProfileAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_profiles.Count > 0);
        }

        public Task<LocalUserProfile?> FindByLoginAsync(string login, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_profiles.FirstOrDefault(profile => profile.Login == login));
        }

        public Task AddAsync(LocalUserProfile profile, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _profiles.Add(profile);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(LocalUserProfile profile, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = _profiles.FindIndex(existing => existing.Id == profile.Id);
            _profiles[index] = profile;
            UpdatedProfiles.Add(profile);
            return Task.CompletedTask;
        }
    }

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public PasswordCredential Hash(string password)
        {
            return new PasswordCredential("test", [1, 2, 3], System.Text.Encoding.UTF8.GetBytes("hashed:" + password), 1, 1);
        }

        public bool Verify(string password, PasswordCredential credential)
        {
            return credential.Hash.SequenceEqual(System.Text.Encoding.UTF8.GetBytes("hashed:" + password));
        }
    }
}

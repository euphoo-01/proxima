using Proxima.Application.Auth;

namespace Proxima.Infrastructure.Auth;

public static class ProximaAuthComposition
{
    public static ILocalAuthService CreateLocalAuthService(string profileStorePath)
    {
        return new LocalAuthService(
            new JsonLocalUserRepository(profileStorePath),
            new Pbkdf2PasswordHasher(),
            new PasswordPolicyValidator());
    }

    public static string GetDefaultProfileStorePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Proxima", "profile-store.json");
    }
}

using Proxima.Application.Settings;

namespace Proxima.Infrastructure.Settings;

public static class ProximaSettingsComposition
{
    public static ISettingsService CreateSettingsService(string storePath)
    {
        return new SettingsService(new JsonUserSettingsRepository(storePath));
    }

    public static string GetDefaultSettingsStorePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Proxima", "settings.json");
    }
}

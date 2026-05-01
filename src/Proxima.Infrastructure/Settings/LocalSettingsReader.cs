using System.Text;
using System.Text.Json;
using Proxima.Application.Settings;

namespace Proxima.Infrastructure.Settings;

public sealed class LocalSettingsReader(string settingsFilePath)
{
    private readonly string _settingsFilePath = settingsFilePath;

    public async Task<UserSettings?> TryReadPrimaryAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFilePath))
        {
            return null;
        }

        await using FileStream stream = File.OpenRead(_settingsFilePath);
        List<UserSettings>? all = await JsonSerializer.DeserializeAsync<List<UserSettings>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return all?.FirstOrDefault();
    }

    public static string UnprotectApiKey(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return string.Empty;
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(protectedValue);
            return Encoding.UTF8.GetString(bytes).Trim();
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }
}

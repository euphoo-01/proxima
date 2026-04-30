namespace Proxima.Application.Settings;

public sealed record SettingsOperationResult(bool Succeeded, string Message, UserSettings? Settings)
{
    public static SettingsOperationResult Success(UserSettings settings) => new(true, string.Empty, settings);
    public static SettingsOperationResult Failure(string message) => new(false, message, null);
}

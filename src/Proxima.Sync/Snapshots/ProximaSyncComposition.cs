using System.Reflection;

namespace Proxima.Sync.Snapshots;

public static class ProximaSyncComposition
{
    public static ISnapshotService CreateSnapshotService()
    {
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Proxima");
        string appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        string deviceIdPath = Path.Combine(appData, "device.id");
        Directory.CreateDirectory(appData);
        string deviceId = File.Exists(deviceIdPath) ? File.ReadAllText(deviceIdPath).Trim() : Guid.NewGuid().ToString("N");
        if (!File.Exists(deviceIdPath))
        {
            File.WriteAllText(deviceIdPath, deviceId);
        }

        return new LocalEncryptedSnapshotService(appData, appVersion, deviceId);
    }
}

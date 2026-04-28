using Proxima.Application.Assets;

namespace Proxima.Infrastructure.Assets;

public static class ProximaAssetComposition
{
    public static IAssetService CreateAssetService(string assetStorePath)
    {
        return new AssetService(new JsonAssetRepository(assetStorePath));
    }

    public static string GetDefaultAssetStorePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Proxima", "asset-store.json");
    }
}

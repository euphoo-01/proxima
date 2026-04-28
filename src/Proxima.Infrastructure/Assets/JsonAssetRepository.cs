using System.Text.Json;
using Proxima.Application.Assets;
using Proxima.Domain.Assets;

namespace Proxima.Infrastructure.Assets;

public sealed class JsonAssetRepository(string filePath) : IAssetRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public Task<IReadOnlyList<Asset>> ListByPortfolioAsync(Guid portfolioId, bool includeArchived, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<Asset> query = LoadAssets().Where(asset => asset.PortfolioId == portfolioId);
        if (!includeArchived)
        {
            query = query.Where(asset => !asset.IsArchived);
        }

        return Task.FromResult<IReadOnlyList<Asset>>(query.ToArray());
    }

    public Task<Asset?> FindByIdAsync(Guid portfolioId, Guid assetId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Asset? asset = LoadAssets().FirstOrDefault(item => item.PortfolioId == portfolioId && item.Id == assetId);
        return Task.FromResult(asset);
    }

    public Task AddAsync(Asset asset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<Asset> items = LoadAssets();
        items.Add(asset);
        SaveAssets(items);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Asset asset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<Asset> items = LoadAssets();
        int index = items.FindIndex(item => item.PortfolioId == asset.PortfolioId && item.Id == asset.Id);
        if (index < 0)
        {
            throw new InvalidOperationException("Asset not found.");
        }

        items[index] = asset;
        SaveAssets(items);
        return Task.CompletedTask;
    }

    private List<Asset> LoadAssets()
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        string json = File.ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<Asset>>(json, JsonOptions) ?? [];
    }

    private void SaveAssets(IReadOnlyCollection<Asset> items)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempFile = filePath + ".tmp";
        string json = JsonSerializer.Serialize(items, JsonOptions);
        File.WriteAllText(tempFile, json);
        File.Move(tempFile, filePath, overwrite: true);
    }
}

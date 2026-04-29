using System.Text.Json;
using Proxima.Application.Quotes;

namespace Proxima.Infrastructure.Quotes;

public sealed class JsonQuoteCacheRepository(string filePath) : IQuoteCacheRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public Task<QuoteCacheEntry?> FindLatestByAssetIdAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Load().FirstOrDefault(item => item.AssetId == assetId));
    }

    public Task UpsertLatestAsync(QuoteCacheEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<QuoteCacheEntry> items = Load();
        int index = items.FindIndex(item => item.AssetId == entry.AssetId);
        if (index >= 0)
        {
            items[index] = entry;
        }
        else
        {
            items.Add(entry);
        }

        Save(items);
        return Task.CompletedTask;
    }

    private List<QuoteCacheEntry> Load()
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

        return JsonSerializer.Deserialize<List<QuoteCacheEntry>>(json, JsonOptions) ?? [];
    }

    private void Save(IReadOnlyCollection<QuoteCacheEntry> items)
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

using System.Text.Json;
using Proxima.Application.Settings;

namespace Proxima.Infrastructure.Settings;

public sealed class JsonUserSettingsRepository(string filePath) : IUserSettingsRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _filePath = filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<UserSettings?> FindByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<UserSettings> all = await ReadAllInternalAsync(cancellationToken).ConfigureAwait(false);
            return all.FirstOrDefault(item => item.OwnerUserId == ownerUserId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpsertAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<UserSettings> all = await ReadAllInternalAsync(cancellationToken).ConfigureAwait(false);
            int idx = all.FindIndex(item => item.OwnerUserId == settings.OwnerUserId);
            if (idx >= 0)
            {
                all[idx] = settings;
            }
            else
            {
                all.Add(settings);
            }

            string dir = Path.GetDirectoryName(_filePath) ?? ".";
            Directory.CreateDirectory(dir);
            await using FileStream stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, all, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<UserSettings>> ReadAllInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using FileStream stream = File.OpenRead(_filePath);
        List<UserSettings>? result = await JsonSerializer.DeserializeAsync<List<UserSettings>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return result ?? [];
    }
}

using System.Text.Json;
using Proxima.Application.Auth;
using Proxima.Domain.Auth;

namespace Proxima.Infrastructure.Auth;

public sealed class JsonLocalUserRepository(string filePath) : ILocalUserRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public Task<bool> HasAnyProfileAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LoadProfiles().Count > 0);
    }

    public Task<LocalUserProfile?> FindByLoginAsync(string login, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LocalUserProfile? profile = LoadProfiles()
            .FirstOrDefault(user => string.Equals(user.Login, login, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(profile);
    }

    public Task AddAsync(LocalUserProfile profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<LocalUserProfile> profiles = LoadProfiles();
        if (profiles.Any(user => string.Equals(user.Login, profile.Login, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A local profile with the same login already exists.");
        }

        profiles.Add(profile);
        SaveProfiles(profiles);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(LocalUserProfile profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<LocalUserProfile> profiles = LoadProfiles();
        int index = profiles.FindIndex(user => user.Id == profile.Id);
        if (index < 0)
        {
            throw new InvalidOperationException("Local profile does not exist.");
        }

        profiles[index] = profile;
        SaveProfiles(profiles);
        return Task.CompletedTask;
    }

    private List<LocalUserProfile> LoadProfiles()
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

        return JsonSerializer.Deserialize<List<LocalUserProfile>>(json, JsonOptions) ?? [];
    }

    private void SaveProfiles(IReadOnlyCollection<LocalUserProfile> profiles)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempFile = filePath + ".tmp";
        string json = JsonSerializer.Serialize(profiles, JsonOptions);
        File.WriteAllText(tempFile, json);
        File.Move(tempFile, filePath, overwrite: true);
    }
}

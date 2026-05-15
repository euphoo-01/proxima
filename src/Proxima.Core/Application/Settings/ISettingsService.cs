namespace Proxima.Core.Application.Settings;

public interface ISettingsService
{
    Task<UserSettings> EnsureAsync(CreateDefaultSettingsRequest request, CancellationToken cancellationToken = default);

    Task<UserSettings?> GetAsync(Guid ownerUserId, CancellationToken cancellationToken = default);

    Task<SettingsOperationResult> UpdateAsync(UpdateSettingsRequest request, CancellationToken cancellationToken = default);
}

namespace Proxima.Application.Settings;

public interface IUserSettingsRepository
{
    Task<UserSettings?> FindByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default);

    Task UpsertAsync(UserSettings settings, CancellationToken cancellationToken = default);
}

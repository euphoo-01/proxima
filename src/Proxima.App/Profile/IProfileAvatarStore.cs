namespace Proxima.App.Profile;

public interface IProfileAvatarStore
{
    byte[]? ReadAvatar(Guid userId);

    Task SaveAvatarAsync(Guid userId, string sourcePath, CancellationToken cancellationToken = default);

    void DeleteAvatar(Guid userId);
}

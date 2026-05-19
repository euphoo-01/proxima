namespace Proxima.App.Profile;

public sealed class FileProfileAvatarStore : IProfileAvatarStore
{
    public byte[]? ReadAvatar(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        string path = GetAvatarPath(userId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task SaveAvatarAsync(Guid userId, string sourcePath, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("User id is required to save profile avatar.");
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Avatar source file was not found.", sourcePath);
        }

        Directory.CreateDirectory(GetProfileExtrasDirectory());
        string targetPath = GetAvatarPath(userId);

        await using FileStream input = File.OpenRead(sourcePath);
        await using FileStream output = File.Create(targetPath);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    public void DeleteAvatar(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        string path = GetAvatarPath(userId);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }
    }

    private static string GetProfileExtrasDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Proxima",
            "Profile");
    }

    private static string GetAvatarPath(Guid userId)
    {
        return Path.Combine(GetProfileExtrasDirectory(), $"{userId:N}.avatar");
    }
}

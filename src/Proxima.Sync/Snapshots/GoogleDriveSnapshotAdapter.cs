namespace Proxima.Sync.Snapshots;

public interface IGoogleDriveSnapshotAdapter
{
    Task<SnapshotImportResult> UploadEncryptedSnapshotAsync(string localSnapshotPath, CancellationToken cancellationToken = default);

    Task<SnapshotImportResult> DownloadEncryptedSnapshotAsync(string remoteSnapshotId, string destinationPath, CancellationToken cancellationToken = default);
}

public sealed class StubGoogleDriveSnapshotAdapter : IGoogleDriveSnapshotAdapter
{
    public Task<SnapshotImportResult> UploadEncryptedSnapshotAsync(string localSnapshotPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SnapshotImportResult(false, "Google Drive sync is not configured in this build.", SnapshotConflictKind.None));
    }

    public Task<SnapshotImportResult> DownloadEncryptedSnapshotAsync(string remoteSnapshotId, string destinationPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SnapshotImportResult(false, "Google Drive sync is not configured in this build.", SnapshotConflictKind.None));
    }
}

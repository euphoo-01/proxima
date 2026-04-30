namespace Proxima.Sync.Snapshots;

public interface ISnapshotService
{
    Task<SnapshotExportResult> ExportAsync(string password, CancellationToken cancellationToken = default);

    Task<SnapshotPreviewResult> PreviewImportAsync(string snapshotPath, string password, CancellationToken cancellationToken = default);

    Task<SnapshotImportResult> ImportAsync(string snapshotPath, string password, bool allowConflictOverride, CancellationToken cancellationToken = default);
}

namespace Proxima.Sync.Snapshots;

public sealed record SnapshotExportResult(bool Succeeded, string Message, string? FilePath, DateTimeOffset? CreatedAtUtc);

public sealed record SnapshotPreviewResult(
    bool Succeeded,
    string Message,
    string? AppVersion,
    int SchemaVersion,
    DateTimeOffset? CreatedAtUtc,
    string? SourceDeviceId,
    SnapshotConflictKind ConflictKind);

public sealed record SnapshotImportResult(bool Succeeded, string Message, SnapshotConflictKind ConflictKind);

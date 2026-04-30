namespace Proxima.Sync.Snapshots;

public enum SnapshotConflictKind
{
    None = 0,
    OlderThanCurrent = 1,
    SameDeviceStale = 2,
    SchemaMismatch = 3,
}

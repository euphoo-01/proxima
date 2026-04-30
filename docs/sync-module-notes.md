# Module 15 Sync Notes (Encrypted Snapshots)

## Implemented

- Encrypted local snapshot export/import service in `Proxima.Sync`:
  - `LocalEncryptedSnapshotService`
  - metadata with schema/app-version/timestamp/device-id/checksum
  - GZip compression + AES-256-GCM encryption
  - key derivation via PBKDF2-SHA256
- Conflict detection:
  - older snapshot
  - same-device stale snapshot
  - schema mismatch
- Settings page sync flow now uses real snapshot service:
  - export snapshot
  - import snapshot (with conflict override checkbox)
  - user-visible status messages
- Google Drive adapter abstraction added with explicit stub.

## Security Notes

- Snapshot files use `.pxsnap` and do not expose portfolio names in filename.
- Wrong password or tampered snapshot fails safely.
- Raw DB folder sync is not offered in UI.

## Deferred

- Real Google Drive OAuth upload/download implementation.
- Strong secret storage for snapshot password beyond session input.
- Full transactional DB restore semantics once PostgreSQL module is fully wired.

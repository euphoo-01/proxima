# Security & Privacy

## Core Principle

Proxima is local-first and privacy-first. Financial data must remain under user control.

## Threat Model

Primary threats:

- stolen laptop;
- malware reading app config/logs;
- accidental cloud upload of plaintext data;
- leaked API keys;
- corrupted sync;
- malicious broker report file;
- dependency vulnerability;
- user mistake during import.

## Authentication / Unlock

Use local password gate.

Implementation requirements:

- Passwords hashed with Argon2id or PBKDF2 with strong parameters if Argon2 unavailable.
- Unique salt per profile.
- No plaintext password.
- Use constant-time comparison.
- Lock app after inactivity if implemented.
- First-run setup requires password confirmation.

## Encryption

Because PostgreSQL local encryption is not simple by default, use a practical layered approach:

1. **Application-level encryption** for sensitive fields:
   - notes;
   - raw import payloads;
   - tokens;
   - sync metadata;
   - personal profile fields if needed.
2. **Encrypted snapshots** for backup/sync.
3. **OS-level disk encryption recommendation** in production docs.
4. **No plaintext backup**.

Recommended algorithm:

- AES-256-GCM for payload encryption.
- Key derived from user password via Argon2id/PBKDF2.
- Random nonce per encrypted payload.
- Store key metadata but never raw key.

## Secrets

- Do not commit API keys.
- Use user secrets/environment variables for dev.
- Store OAuth tokens/API keys through OS keychain where available.
- Redact secrets in logs.

## Network Privacy

Allowed external calls:

- quote providers;
- NBRB exchange rates;
- Google Drive encrypted snapshot sync;
- optional update checks if explicitly implemented.

Forbidden:

- sending portfolio composition;
- sending transaction history;
- sending broker PDFs;
- sending tax reports;
- telemetry without opt-in;
- crash reports with financial data.

## Import Security

Broker files are untrusted input.

Requirements:

- file size limit;
- extension/content checks;
- parser sandbox by design: no script execution;
- safe PDF parsing library;
- no macro execution;
- store raw import encrypted;
- show suspicious rows to user.

## Google Drive Sync

Do not sync raw DB.

Implement:

- encrypted `.proxima-snapshot` files;
- checksum;
- app version;
- schema version;
- created_at;
- source device id;
- conflict detection.

Snapshot format draft:

```json
{
  "format": "proxima.snapshot.v1",
  "schemaVersion": "1.0.0",
  "appVersion": "0.1.0",
  "createdAt": "...",
  "sourceDeviceId": "...",
  "payload": "AES-GCM encrypted compressed JSON"
}
```

## Logging

Allowed:

- high-level events;
- operation duration;
- import row counts;
- provider name;
- non-sensitive errors.

Forbidden:

- raw transaction payloads;
- imported file content;
- passwords;
- token values;
- full connection string;
- exact portfolio values unless explicitly redacted.

## Tax Disclaimer

Tax module must show:

> Расчет носит информационный характер. Перед подачей декларации проверьте актуальные правила и официальные источники.

Tax rules must be versioned and editable/configurable. Do not hardcode outdated rules as permanent truth.

## Dependency Security

- Use maintained packages.
- Run `dotnet list package --vulnerable`.
- Pin major versions.
- Avoid abandoned cryptography packages.
- Prefer Microsoft/BCL crypto APIs or widely used vetted libraries.

## Security Acceptance Checklist

- [ ] Password hash is not plaintext.
- [ ] No secrets in repository.
- [ ] No financial data sent to telemetry.
- [ ] Import raw payload encrypted.
- [ ] Sync snapshot encrypted.
- [ ] Logs redacted.
- [ ] App works offline.
- [ ] Failure states are visible and safe.

# Password credential refactor

## What changed

- Replaced split password metadata columns with one self-contained `users.password_hash text` column.
- Removed these runtime model fields from `UserEntity`:
  - `PasswordAlgorithm`
  - `PasswordSalt`
  - `PasswordIterations`
  - `PasswordVersion`
- Replaced `PasswordCredential(algorithm, salt, hash, iterations, version)` with `PasswordCredential(encodedHash)`.
- Updated `Pbkdf2PasswordHasher` to store one encoded string:

```text
$pbkdf2-sha256$v=1$i=210000$<base64-salt>$<base64-derived-hash>
```

## Why this is safer and cleaner

The database no longer exposes password hashing metadata as separate columns, while the application still preserves the required cryptographic parameters for safe verification.

The stored value is not a plain SHA hash and does not contain the plaintext password. It is a self-contained encoded PBKDF2 credential containing algorithm, version, work factor, salt and derived hash.

## Migration behavior

`20260515022000_EncodePasswordCredential` converts existing rows from the old split-column format into the encoded string, then drops the old metadata columns.

Fresh databases created from `20260515020000_InitialPostgreSqlSchema` now create only:

```sql
password_hash text not null default ''
```

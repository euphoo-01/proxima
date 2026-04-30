using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Proxima.Sync.Snapshots;

public sealed class LocalEncryptedSnapshotService : ISnapshotService
{
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };
    private readonly string _appDataDir;
    private readonly string _snapshotDir;
    private readonly string _statePath;
    private readonly string _appVersion;
    private readonly string _sourceDeviceId;

    public LocalEncryptedSnapshotService(string appDataDir, string appVersion, string sourceDeviceId)
    {
        _appDataDir = appDataDir;
        _snapshotDir = Path.Combine(appDataDir, "snapshots");
        _statePath = Path.Combine(_snapshotDir, "snapshot-state.json");
        _appVersion = appVersion;
        _sourceDeviceId = sourceDeviceId;
    }

    public async Task<SnapshotExportResult> ExportAsync(string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return new SnapshotExportResult(false, "Введите пароль snapshot.", null, null);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(_snapshotDir);
            DateTimeOffset createdAt = DateTimeOffset.UtcNow;
            string payloadJson = BuildPayloadJson(cancellationToken);
            byte[] compressed = Compress(Encoding.UTF8.GetBytes(payloadJson));
            byte[] checksum = SHA256.HashData(compressed);

            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] key = DeriveKey(password, salt);
            byte[] tag = new byte[16];
            byte[] cipher = new byte[compressed.Length];
            using (AesGcm aes = new(key, 16))
            {
                aes.Encrypt(nonce, compressed, cipher, tag);
            }

            SnapshotFile file = new(
                CurrentSchemaVersion,
                _appVersion,
                createdAt,
                _sourceDeviceId,
                Convert.ToBase64String(checksum),
                Convert.ToBase64String(salt),
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(tag),
                Convert.ToBase64String(cipher));

            string name = $"snapshot-{createdAt:yyyyMMdd-HHmmss}.pxsnap";
            string path = Path.Combine(_snapshotDir, name);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(file, JsonOptions), cancellationToken).ConfigureAwait(false);
            await SaveStateAsync(new SnapshotState(createdAt, _sourceDeviceId), cancellationToken).ConfigureAwait(false);

            return new SnapshotExportResult(true, "Encrypted snapshot exported.", path, createdAt);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return new SnapshotExportResult(false, $"Snapshot export failed: {ex.Message}", null, null);
        }
    }

    public async Task<SnapshotPreviewResult> PreviewImportAsync(string snapshotPath, string password, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(snapshotPath))
        {
            return new SnapshotPreviewResult(false, "Файл snapshot не найден.", null, 0, null, null, SnapshotConflictKind.None);
        }

        try
        {
            SnapshotFile file = await ReadSnapshotFileAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
            byte[] compressed = DecryptCompressed(file, password);
            byte[] checksum = SHA256.HashData(compressed);
            string checksumBase64 = Convert.ToBase64String(checksum);
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromBase64String(file.ChecksumBase64),
                    Convert.FromBase64String(checksumBase64)))
            {
                return new SnapshotPreviewResult(false, "Snapshot checksum mismatch.", file.AppVersion, file.SchemaVersion, file.CreatedAtUtc, file.SourceDeviceId, SnapshotConflictKind.None);
            }

            if (file.SchemaVersion != CurrentSchemaVersion)
            {
                return new SnapshotPreviewResult(false, "Schema mismatch.", file.AppVersion, file.SchemaVersion, file.CreatedAtUtc, file.SourceDeviceId, SnapshotConflictKind.SchemaMismatch);
            }

            SnapshotConflictKind conflict = await DetectConflictAsync(file, cancellationToken).ConfigureAwait(false);
            string message = conflict == SnapshotConflictKind.None ? "Snapshot preview ready." : $"Conflict detected: {conflict}.";
            return new SnapshotPreviewResult(true, message, file.AppVersion, file.SchemaVersion, file.CreatedAtUtc, file.SourceDeviceId, conflict);
        }
        catch (CryptographicException)
        {
            return new SnapshotPreviewResult(false, "Неверный пароль snapshot или поврежденный файл.", null, 0, null, null, SnapshotConflictKind.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException)
        {
            return new SnapshotPreviewResult(false, $"Snapshot preview failed: {ex.Message}", null, 0, null, null, SnapshotConflictKind.None);
        }
    }

    public async Task<SnapshotImportResult> ImportAsync(string snapshotPath, string password, bool allowConflictOverride, CancellationToken cancellationToken = default)
    {
        SnapshotPreviewResult preview = await PreviewImportAsync(snapshotPath, password, cancellationToken).ConfigureAwait(false);
        if (!preview.Succeeded)
        {
            return new SnapshotImportResult(false, preview.Message, preview.ConflictKind);
        }

        if (preview.ConflictKind != SnapshotConflictKind.None && !allowConflictOverride)
        {
            return new SnapshotImportResult(false, "Import stopped due to conflict warning.", preview.ConflictKind);
        }

        try
        {
            SnapshotFile file = await ReadSnapshotFileAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
            byte[] compressed = DecryptCompressed(file, password);
            string payloadJson = Encoding.UTF8.GetString(Decompress(compressed));
            SnapshotPayload payload = JsonSerializer.Deserialize<SnapshotPayload>(payloadJson, JsonOptions)
                ?? throw new JsonException("Invalid payload.");

            string tempDir = Path.Combine(_appDataDir, ".snapshot-import-temp");
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
            Directory.CreateDirectory(tempDir);

            foreach (SnapshotEntry entry in payload.Entries)
            {
                string safe = Path.GetFileName(entry.RelativePath);
                if (string.IsNullOrWhiteSpace(safe))
                {
                    continue;
                }

                string target = Path.Combine(tempDir, safe);
                await File.WriteAllTextAsync(target, entry.Content, cancellationToken).ConfigureAwait(false);
            }

            foreach (SnapshotEntry entry in payload.Entries)
            {
                string safe = Path.GetFileName(entry.RelativePath);
                if (string.IsNullOrWhiteSpace(safe))
                {
                    continue;
                }

                string source = Path.Combine(tempDir, safe);
                string target = Path.Combine(_appDataDir, safe);
                File.Copy(source, target, overwrite: true);
            }

            await SaveStateAsync(new SnapshotState(file.CreatedAtUtc, file.SourceDeviceId), cancellationToken).ConfigureAwait(false);
            Directory.Delete(tempDir, recursive: true);
            return new SnapshotImportResult(true, "Snapshot imported.", preview.ConflictKind);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or CryptographicException)
        {
            return new SnapshotImportResult(false, $"Snapshot import failed: {ex.Message}", SnapshotConflictKind.None);
        }
    }

    private string BuildPayloadJson(CancellationToken cancellationToken)
    {
        List<SnapshotEntry> entries = [];
        foreach (string file in EnumerateDataFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(file))
            {
                continue;
            }

            entries.Add(new SnapshotEntry(Path.GetFileName(file), File.ReadAllText(file)));
        }

        SnapshotPayload payload = new(CurrentSchemaVersion, _appVersion, DateTimeOffset.UtcNow, _sourceDeviceId, entries);
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private IEnumerable<string> EnumerateDataFiles()
    {
        yield return Path.Combine(_appDataDir, "profiles.json");
        yield return Path.Combine(_appDataDir, "portfolios.json");
        yield return Path.Combine(_appDataDir, "assets.json");
        yield return Path.Combine(_appDataDir, "transactions.json");
        yield return Path.Combine(_appDataDir, "goals.json");
        yield return Path.Combine(_appDataDir, "settings.json");
        yield return Path.Combine(_appDataDir, "quote-cache.json");
    }

    private async Task<SnapshotFile> ReadSnapshotFileAsync(string snapshotPath, CancellationToken cancellationToken)
    {
        string json = await File.ReadAllTextAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<SnapshotFile>(json, JsonOptions) ?? throw new JsonException("Invalid snapshot format.");
    }

    private async Task<SnapshotConflictKind> DetectConflictAsync(SnapshotFile file, CancellationToken cancellationToken)
    {
        SnapshotState? state = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            return SnapshotConflictKind.None;
        }

        if (file.CreatedAtUtc < state.LastImportedOrExportedAtUtc)
        {
            return file.SourceDeviceId == state.LastKnownDeviceId
                ? SnapshotConflictKind.SameDeviceStale
                : SnapshotConflictKind.OlderThanCurrent;
        }

        return SnapshotConflictKind.None;
    }

    private async Task<SnapshotState?> LoadStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_statePath))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(_statePath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<SnapshotState>(json, JsonOptions);
    }

    private async Task SaveStateAsync(SnapshotState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_snapshotDir);
        string json = JsonSerializer.Serialize(state, JsonOptions);
        await File.WriteAllTextAsync(_statePath, json, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, 120_000, HashAlgorithmName.SHA256, 32);
    }

    private static byte[] Compress(byte[] input)
    {
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(input, 0, input.Length);
        }
        return output.ToArray();
    }

    private static byte[] Decompress(byte[] input)
    {
        using MemoryStream source = new(input);
        using GZipStream gzip = new(source, CompressionMode.Decompress);
        using MemoryStream output = new();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] DecryptCompressed(SnapshotFile file, string password)
    {
        byte[] salt = Convert.FromBase64String(file.SaltBase64);
        byte[] nonce = Convert.FromBase64String(file.NonceBase64);
        byte[] tag = Convert.FromBase64String(file.TagBase64);
        byte[] cipher = Convert.FromBase64String(file.CiphertextBase64);
        byte[] key = DeriveKey(password, salt);
        byte[] plain = new byte[cipher.Length];
        using (AesGcm aes = new(key, 16))
        {
            aes.Decrypt(nonce, cipher, tag, plain);
        }
        return plain;
    }

    private sealed record SnapshotFile(
        int SchemaVersion,
        string AppVersion,
        DateTimeOffset CreatedAtUtc,
        string SourceDeviceId,
        string ChecksumBase64,
        string SaltBase64,
        string NonceBase64,
        string TagBase64,
        string CiphertextBase64);

    private sealed record SnapshotPayload(
        int SchemaVersion,
        string AppVersion,
        DateTimeOffset CreatedAtUtc,
        string SourceDeviceId,
        List<SnapshotEntry> Entries);

    private sealed record SnapshotEntry(string RelativePath, string Content);
    private sealed record SnapshotState(DateTimeOffset LastImportedOrExportedAtUtc, string LastKnownDeviceId);
}

using System.Text.Json;
using Proxima.Application.Observability;

namespace Proxima.Infrastructure.Persistence;

public sealed class JsonAuditLogRepository(string filePath) : IAuditLogRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<AuditEvent> all = await ReadAllInternalAsync(cancellationToken).ConfigureAwait(false);
            all.Add(auditEvent);
            string directory = Path.GetDirectoryName(filePath) ?? ".";
            Directory.CreateDirectory(directory);
            await using FileStream stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, all, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<AuditEvent>> ReadAllInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using FileStream stream = File.OpenRead(filePath);
        List<AuditEvent>? result = await JsonSerializer.DeserializeAsync<List<AuditEvent>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return result ?? [];
    }
}

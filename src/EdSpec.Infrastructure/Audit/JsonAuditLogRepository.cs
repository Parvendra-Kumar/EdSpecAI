using System.Text.Json;
using EdSpec.Application.Audit;
using EdSpec.Domain.Audit;

namespace EdSpec.Infrastructure.Audit;

public sealed class JsonAuditLogRepository : IAuditLogRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonAuditLogRepository(string applicationRootPath)
    {
        _filePath = Path.Combine(applicationRootPath, "audit-log.json");
    }

    public async Task<AuditLogEntry> CreateAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadStoreAsync(cancellationToken);
            entries.Add(entry);
            await WriteStoreAsync(entries, cancellationToken);

            return entry;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<AuditLogEntry>> ReadStoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var store = await JsonSerializer.DeserializeAsync<AuditLogStore>(stream, SerializerOptions, cancellationToken);

        return store?.Entries ?? [];
    }

    private async Task WriteStoreAsync(List<AuditLogEntry> entries, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_filePath);
        var store = new AuditLogStore(entries.OrderBy(entry => entry.CreatedAt).ToList());
        await JsonSerializer.SerializeAsync(stream, store, SerializerOptions, cancellationToken);
    }

    private sealed record AuditLogStore(List<AuditLogEntry> Entries);
}

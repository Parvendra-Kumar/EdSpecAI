using System.Text.Json;
using EdSpec.Application.Specifications;
using EdSpec.Domain.Specifications;

namespace EdSpec.Infrastructure.Specifications;

public sealed class JsonSpecificationDraftRepository : ISpecificationDraftRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonSpecificationDraftRepository(string applicationRootPath)
    {
        _filePath = Path.Combine(applicationRootPath, "specifications.json");
    }

    public async Task<IReadOnlyCollection<SpecificationDraft>> GetAllAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await ReadStoreAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<SpecificationDraft?> GetAsync(string id, string version, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var drafts = await ReadStoreAsync(cancellationToken);
            return drafts.FirstOrDefault(draft =>
                string.Equals(draft.Id, id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(draft.Version, version, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<SpecificationDraft> CreateAsync(SpecificationDraft draft, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var drafts = await ReadStoreAsync(cancellationToken);
            if (ContainsVersion(drafts, draft.Id, draft.Version))
            {
                throw new DuplicateSpecificationVersionException(draft.Id, draft.Version);
            }

            drafts.Add(draft);
            await WriteStoreAsync(drafts, cancellationToken);

            return draft;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<SpecificationDraft> UpdateAsync(SpecificationDraft draft, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var drafts = await ReadStoreAsync(cancellationToken);
            var existingIndex = drafts.FindIndex(existing =>
                string.Equals(existing.Id, draft.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Version, draft.Version, StringComparison.OrdinalIgnoreCase));

            if (existingIndex < 0)
            {
                throw new SpecificationDraftNotFoundException(draft.Id, draft.Version);
            }

            drafts[existingIndex] = draft;
            await WriteStoreAsync(drafts, cancellationToken);

            return draft;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id, string version, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var drafts = await ReadStoreAsync(cancellationToken);
            var existingIndex = drafts.FindIndex(existing =>
                string.Equals(existing.Id, id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Version, version, StringComparison.OrdinalIgnoreCase));

            if (existingIndex < 0)
            {
                return false;
            }

            drafts.RemoveAt(existingIndex);
            await WriteStoreAsync(drafts, cancellationToken);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<SpecificationDraft>> ReadStoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var store = await JsonSerializer.DeserializeAsync<SpecificationStore>(stream, SerializerOptions, cancellationToken);

        return store?.Specifications ?? [];
    }

    private async Task WriteStoreAsync(List<SpecificationDraft> drafts, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_filePath);
        var store = new SpecificationStore(drafts.OrderBy(draft => draft.Id).ThenBy(draft => draft.Version).ToList());
        await JsonSerializer.SerializeAsync(stream, store, SerializerOptions, cancellationToken);
    }

    private static bool ContainsVersion(IEnumerable<SpecificationDraft> drafts, string id, string version)
    {
        return drafts.Any(draft =>
            string.Equals(draft.Id, id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(draft.Version, version, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record SpecificationStore(List<SpecificationDraft> Specifications);
}

public sealed class DuplicateSpecificationVersionException(string id, string version)
    : InvalidOperationException($"Specification '{id}' version '{version}' already exists.");

public sealed class SpecificationDraftNotFoundException(string id, string version)
    : InvalidOperationException($"Specification '{id}' version '{version}' was not found.");

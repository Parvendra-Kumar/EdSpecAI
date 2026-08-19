using System.Text.Json;
using EdSpec.Application.Assessments;
using EdSpec.Domain.Assessments;

namespace EdSpec.Infrastructure.Assessments;

public sealed class JsonGeneratedAssessmentRepository : IGeneratedAssessmentRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonGeneratedAssessmentRepository(string applicationRootPath)
    {
        _filePath = Path.Combine(applicationRootPath, "assessments.json");
    }

    public async Task<IReadOnlyCollection<GeneratedAssessment>> GetAllAsync(CancellationToken cancellationToken)
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

    public async Task<GeneratedAssessment?> GetAsync(string id, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var assessments = await ReadStoreAsync(cancellationToken);
            return assessments.FirstOrDefault(assessment =>
                string.Equals(assessment.Id, id, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<GeneratedAssessment> CreateAsync(GeneratedAssessment assessment, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var assessments = await ReadStoreAsync(cancellationToken);
            assessments.Add(assessment);
            await WriteStoreAsync(assessments, cancellationToken);

            return assessment;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<GeneratedAssessment>> ReadStoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var store = await JsonSerializer.DeserializeAsync<AssessmentStore>(stream, SerializerOptions, cancellationToken);

        return store?.Assessments ?? [];
    }

    private async Task WriteStoreAsync(List<GeneratedAssessment> assessments, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_filePath);
        var store = new AssessmentStore(assessments.OrderBy(assessment => assessment.CreatedAt).ToList());
        await JsonSerializer.SerializeAsync(stream, store, SerializerOptions, cancellationToken);
    }

    private sealed record AssessmentStore(List<GeneratedAssessment> Assessments);
}

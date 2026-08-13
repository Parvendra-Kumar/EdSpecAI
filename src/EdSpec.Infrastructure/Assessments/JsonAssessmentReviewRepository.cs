using System.Text.Json;
using EdSpec.Application.Assessments;
using EdSpec.Domain.Assessments;

namespace EdSpec.Infrastructure.Assessments;

public sealed class JsonAssessmentReviewRepository : IAssessmentReviewRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonAssessmentReviewRepository(string applicationRootPath)
    {
        _filePath = Path.Combine(applicationRootPath, "assessment-reviews.json");
    }

    public async Task<AssessmentReview> CreateAsync(AssessmentReview review, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var reviews = await ReadStoreAsync(cancellationToken);
            reviews.Add(review);
            await WriteStoreAsync(reviews, cancellationToken);

            return review;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<AssessmentReview>> ReadStoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var store = await JsonSerializer.DeserializeAsync<AssessmentReviewStore>(stream, SerializerOptions, cancellationToken);

        return store?.Reviews ?? [];
    }

    private async Task WriteStoreAsync(List<AssessmentReview> reviews, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_filePath);
        var store = new AssessmentReviewStore(reviews.OrderBy(review => review.CreatedAt).ToList());
        await JsonSerializer.SerializeAsync(stream, store, SerializerOptions, cancellationToken);
    }

    private sealed record AssessmentReviewStore(List<AssessmentReview> Reviews);
}

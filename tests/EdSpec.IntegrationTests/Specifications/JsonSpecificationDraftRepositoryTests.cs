using EdSpec.Domain.Specifications;
using EdSpec.Infrastructure.Specifications;

namespace EdSpec.IntegrationTests.Specifications;

public sealed class JsonSpecificationDraftRepositoryTests
{
    [Fact]
    public async Task UpdateAsync_PersistsUpdatedSpecification()
    {
        var root = Path.Combine(Path.GetTempPath(), $"edspec-specifications-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var repository = new JsonSpecificationDraftRepository(root);
            var original = new SpecificationDraft(
                "persisted-specification",
                "1.0.0",
                "draft",
                "Original title",
                "Mathematics",
                "Demonstrate persistence of specification updates",
                new QuestionRules(1, "multiple-choice", 4),
                new DifficultyDistribution(1, 0, 0),
                new ScoringRules(2, 2),
                new ApprovalInfo(true, null, null),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);

            await repository.CreateAsync(original, CancellationToken.None);
            await repository.UpdateAsync(original with
            {
                Title = "Updated title",
                LearningObjective = "Demonstrate that specification updates are persisted"
            }, CancellationToken.None);

            var reloaded = await repository.GetAsync(original.Id, original.Version, CancellationToken.None);

            Assert.NotNull(reloaded);
            Assert.Equal("Updated title", reloaded.Title);
            Assert.Equal("Demonstrate that specification updates are persisted", reloaded.LearningObjective);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

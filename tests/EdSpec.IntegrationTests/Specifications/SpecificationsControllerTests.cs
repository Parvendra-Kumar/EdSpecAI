using EdSpec.Api.Controllers;
using EdSpec.Application.Audit;
using EdSpec.Application.Specifications;
using EdSpec.Domain.Audit;
using EdSpec.Domain.Specifications;
using Microsoft.AspNetCore.Mvc;

namespace EdSpec.IntegrationTests.Specifications;

public sealed class SpecificationsControllerTests
{
    [Fact]
    public async Task GetSpecifications_ReturnsStoredSpecificationsOrderedByTitleAndUpdatedDate()
    {
        var olderAlgebra = CreateSpecification("algebra", "1.0.0", "Algebra", DateTimeOffset.UtcNow.AddDays(-2));
        var newerAlgebra = CreateSpecification("algebra", "2.0.0", "Algebra", DateTimeOffset.UtcNow.AddDays(-1));
        var science = CreateSpecification("science", "1.0.0", "Science", DateTimeOffset.UtcNow);
        var controller = new SpecificationsController(
            new FakeSpecificationDraftRepository([newerAlgebra, science, olderAlgebra]),
            new FakeAuditLogRepository());

        var result = await controller.GetSpecifications(CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var specifications = Assert.IsAssignableFrom<IReadOnlyCollection<SpecificationDraft>>(response.Value);
        Assert.Equal(["Algebra:2.0.0", "Algebra:1.0.0", "Science:1.0.0"], specifications.Select(item => $"{item.Title}:{item.Version}"));
    }

    [Fact]
    public async Task UpdateDraft_MovesApprovedSpecificationBackToDraftAndClearsApproval()
    {
        var existing = CreateSpecification("algebra", "1.0.0", "Algebra", DateTimeOffset.UtcNow);
        var repository = new FakeSpecificationDraftRepository([existing]);
        var controller = new SpecificationsController(repository, new FakeAuditLogRepository());

        var result = await controller.UpdateDraft(
            "algebra",
            "1.0.0",
            new UpdateDraftSpecificationRequest(
                "Updated Algebra",
                "Mathematics",
                "Solve more advanced linear equations",
                new QuestionRulesRequest(2, "multiple-choice", 4),
                new DifficultyDistributionRequest(1, 1, 0),
                new ScoringRulesRequest(2, 4)),
            CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var updated = Assert.IsType<SpecificationDraft>(response.Value);
        Assert.Equal("draft", updated.Status);
        Assert.Null(updated.Approval.ApprovedBy);
        Assert.Null(updated.Approval.ApprovedAt);
    }

    [Fact]
    public async Task DeleteDraft_RemovesSpecificationAndReturnsNoContent()
    {
        var existing = CreateSpecification("algebra", "1.0.0", "Algebra", DateTimeOffset.UtcNow);
        var repository = new FakeSpecificationDraftRepository([existing]);
        var controller = new SpecificationsController(repository, new FakeAuditLogRepository());

        var result = await controller.DeleteDraft(
            "algebra",
            "1.0.0",
            new DeleteSpecificationRequest("Reviewer"),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(await repository.GetAllAsync(CancellationToken.None));
    }

    private static SpecificationDraft CreateSpecification(string id, string version, string title, DateTimeOffset updatedAt)
    {
        return new SpecificationDraft(
            id,
            version,
            "approved",
            title,
            "Sample Subject",
            "Demonstrate understanding of the approved topic",
            new QuestionRules(1, "multiple-choice", 4),
            new DifficultyDistribution(1, 0, 0),
            new ScoringRules(2, 2),
            new ApprovalInfo(true, "Reviewer", updatedAt),
            updatedAt,
            updatedAt);
    }

    private sealed class FakeSpecificationDraftRepository(IReadOnlyCollection<SpecificationDraft> specifications) : ISpecificationDraftRepository
    {
        private readonly List<SpecificationDraft> _specifications = specifications.ToList();

        public Task<IReadOnlyCollection<SpecificationDraft>> GetAllAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<SpecificationDraft>>(_specifications);
        }

        public Task<SpecificationDraft?> GetAsync(string id, string version, CancellationToken cancellationToken)
        {
            return Task.FromResult(_specifications.FirstOrDefault(item => item.Id == id && item.Version == version));
        }

        public Task<SpecificationDraft> CreateAsync(SpecificationDraft draft, CancellationToken cancellationToken)
        {
            return Task.FromResult(draft);
        }

        public Task<SpecificationDraft> UpdateAsync(SpecificationDraft draft, CancellationToken cancellationToken)
        {
            return Task.FromResult(draft);
        }

        public Task<bool> DeleteAsync(string id, string version, CancellationToken cancellationToken)
        {
            var existing = _specifications.FirstOrDefault(item => item.Id == id && item.Version == version);
            return Task.FromResult(existing is not null && _specifications.Remove(existing));
        }
    }

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public Task<AuditLogEntry> CreateAsync(AuditLogEntry entry, CancellationToken cancellationToken)
        {
            return Task.FromResult(entry);
        }
    }
}

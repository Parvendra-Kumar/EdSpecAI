using EdSpec.Api.Controllers;
using EdSpec.Api.Workflows;
using EdSpec.Application.Assessments;
using EdSpec.Application.Audit;
using EdSpec.Application.Specifications;
using EdSpec.Domain.Assessments;
using EdSpec.Domain.Audit;
using EdSpec.Domain.Specifications;
using EdSpec.Validation.Assessments;
using Microsoft.AspNetCore.Mvc;

namespace EdSpec.IntegrationTests.Assessments;

public sealed class AssessmentsControllerTests
{
    [Fact]
    public async Task Generate_ReturnsNotFound_WhenSpecificationDoesNotExist()
    {
        var controller = CreateController(specification: null);

        var result = await controller.Generate(
            "missing",
            "1.0.0",
            new GenerateAssessmentRequest("POC User"),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Generate_ReturnsBadRequest_WhenSpecificationIsNotApproved()
    {
        var controller = CreateController(CreateSpecification("draft"));

        var result = await controller.Generate(
            "sample-topic-assessment",
            "1.0.0",
            new GenerateAssessmentRequest("POC User"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Generate_ReturnsAndPersistsAssessment_WhenSpecificationIsApproved()
    {
        var repository = new FakeGeneratedAssessmentRepository();
        var controller = CreateController(CreateSpecification("approved"), repository);

        var result = await controller.Generate(
            "sample-topic-assessment",
            "1.0.0",
            new GenerateAssessmentRequest("POC User"),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<GenerateAssessmentResponse>(okResult.Value);
        Assert.Equal("sample-topic-assessment", response.Assessment.SpecificationId);
        Assert.Equal("1.0.0", response.Assessment.SpecificationVersion);
        Assert.Equal("generated", response.Assessment.Status);
        Assert.Equal("passed", response.Review.Status);
        Assert.Single(repository.Assessments);
    }

    private static AssessmentsController CreateController(
        SpecificationDraft? specification,
        FakeGeneratedAssessmentRepository? assessmentRepository = null)
    {
        var auditRepository = new FakeAuditLogRepository();
        var orchestrator = new SemanticKernelAssessmentWorkflowOrchestrator(
            new FakeSpecificationDraftRepository(specification),
            assessmentRepository ?? new FakeGeneratedAssessmentRepository(),
            new FakeAssessmentReviewRepository(),
            auditRepository,
            new FakeAssessmentGenerationAgent(),
            new FakeAssessmentReviewAgent(),
            new GeneratedAssessmentValidator());

        return new AssessmentsController(orchestrator);
    }

    private static SpecificationDraft CreateSpecification(string status)
    {
        return new SpecificationDraft(
            "sample-topic-assessment",
            "1.0.0",
            status,
            "Sample Topic Assessment",
            "Sample Subject",
            "Demonstrate understanding of the approved topic",
            new QuestionRules(1, "multiple-choice", 4),
            new DifficultyDistribution(1, 0, 0),
            new ScoringRules(2, 2),
            new ApprovalInfo(true, "Reviewer", DateTimeOffset.UtcNow),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private sealed class FakeSpecificationDraftRepository(SpecificationDraft? specification) : ISpecificationDraftRepository
    {
        public Task<IReadOnlyCollection<SpecificationDraft>> GetAllAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<SpecificationDraft> specifications = specification is null ? [] : [specification];
            return Task.FromResult(specifications);
        }

        public Task<SpecificationDraft?> GetAsync(string id, string version, CancellationToken cancellationToken)
        {
            return Task.FromResult(specification);
        }

        public Task<SpecificationDraft> CreateAsync(SpecificationDraft draft, CancellationToken cancellationToken)
        {
            return Task.FromResult(draft);
        }

        public Task<SpecificationDraft> UpdateAsync(SpecificationDraft draft, CancellationToken cancellationToken)
        {
            return Task.FromResult(draft);
        }
    }

    private sealed class FakeGeneratedAssessmentRepository : IGeneratedAssessmentRepository
    {
        public List<GeneratedAssessment> Assessments { get; } = [];

        public Task<GeneratedAssessment> CreateAsync(GeneratedAssessment assessment, CancellationToken cancellationToken)
        {
            Assessments.Add(assessment);
            return Task.FromResult(assessment);
        }
    }

    private sealed class FakeAssessmentReviewRepository : IAssessmentReviewRepository
    {
        public Task<AssessmentReview> CreateAsync(AssessmentReview review, CancellationToken cancellationToken)
        {
            return Task.FromResult(review);
        }
    }

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public Task<AuditLogEntry> CreateAsync(AuditLogEntry entry, CancellationToken cancellationToken)
        {
            return Task.FromResult(entry);
        }
    }

    private sealed class FakeAssessmentGenerationAgent : IAssessmentGenerationAgent
    {
        public Task<AssessmentGenerationAgentResult> GenerateAsync(SpecificationDraft specification, CancellationToken cancellationToken)
        {
            IReadOnlyList<GeneratedQuestion> questions =
            [
                new GeneratedQuestion(
                    "q1",
                    specification.LearningObjective,
                    "easy",
                    "multiple-choice",
                    "Which option best matches the approved topic?",
                    [
                        new GeneratedOption("A", "The correct concept"),
                        new GeneratedOption("B", "A distractor"),
                        new GeneratedOption("C", "Another distractor"),
                        new GeneratedOption("D", "An unrelated distractor")
                    ],
                    "A",
                    2)
            ];

            return Task.FromResult(new AssessmentGenerationAgentResult(questions));
        }
    }

    private sealed class FakeAssessmentReviewAgent : IAssessmentReviewAgent
    {
        public Task<AssessmentReview> ReviewAsync(
            SpecificationDraft specification,
            GeneratedAssessment assessment,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new AssessmentReview(
                "review-1",
                assessment.Id,
                specification.Id,
                specification.Version,
                "passed",
                [],
                0.95m,
                DateTimeOffset.UtcNow));
        }
    }
}

using EdSpec.Api.Controllers;
using EdSpec.Application.Assessments;
using EdSpec.Application.Specifications;
using EdSpec.Domain.Assessments;
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
            "algebra-basic",
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
            "algebra-basic",
            "1.0.0",
            new GenerateAssessmentRequest("POC User"),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var assessment = Assert.IsType<GeneratedAssessment>(okResult.Value);
        Assert.Equal("algebra-basic", assessment.SpecificationId);
        Assert.Equal("1.0.0", assessment.SpecificationVersion);
        Assert.Equal("generated", assessment.Status);
        Assert.Single(repository.Assessments);
    }

    private static AssessmentsController CreateController(
        SpecificationDraft? specification,
        FakeGeneratedAssessmentRepository? assessmentRepository = null)
    {
        return new AssessmentsController(
            new FakeSpecificationDraftRepository(specification),
            assessmentRepository ?? new FakeGeneratedAssessmentRepository(),
            new FakeAssessmentGenerationAgent(),
            new GeneratedAssessmentValidator());
    }

    private static SpecificationDraft CreateSpecification(string status)
    {
        return new SpecificationDraft(
            "algebra-basic",
            "1.0.0",
            status,
            "Basic Algebra Assessment",
            "Basic Algebra",
            "Solve single-variable linear equations",
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
                    "Solve x + 2 = 5.",
                    [
                        new GeneratedOption("A", "3"),
                        new GeneratedOption("B", "4"),
                        new GeneratedOption("C", "5"),
                        new GeneratedOption("D", "6")
                    ],
                    "A",
                    2)
            ];

            return Task.FromResult(new AssessmentGenerationAgentResult(questions));
        }
    }
}

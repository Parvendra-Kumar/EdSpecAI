using EdSpec.Domain.Assessments;
using EdSpec.Domain.Specifications;

namespace EdSpec.Application.Assessments;

public sealed record AssessmentGenerationAgentResult(IReadOnlyList<GeneratedQuestion> Questions);

public interface IAssessmentGenerationAgent
{
    Task<AssessmentGenerationAgentResult> GenerateAsync(
        SpecificationDraft specification,
        CancellationToken cancellationToken);
}

public interface IGeneratedAssessmentRepository
{
    Task<GeneratedAssessment> CreateAsync(GeneratedAssessment assessment, CancellationToken cancellationToken);
}

public interface IAssessmentReviewAgent
{
    Task<AssessmentReview> ReviewAsync(
        SpecificationDraft specification,
        GeneratedAssessment assessment,
        CancellationToken cancellationToken);
}

public interface IAssessmentReviewRepository
{
    Task<AssessmentReview> CreateAsync(AssessmentReview review, CancellationToken cancellationToken);
}

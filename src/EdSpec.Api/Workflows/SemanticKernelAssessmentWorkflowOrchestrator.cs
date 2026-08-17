using EdSpec.Agents.Assessments;
using EdSpec.Application.Assessments;
using EdSpec.Application.Audit;
using EdSpec.Application.Specifications;
using EdSpec.Domain.Assessments;
using EdSpec.Domain.Audit;
using EdSpec.Validation.Assessments;

namespace EdSpec.Api.Workflows;

public sealed class SemanticKernelAssessmentWorkflowOrchestrator
{
    private readonly ISpecificationDraftRepository _specificationRepository;
    private readonly IGeneratedAssessmentRepository _assessmentRepository;
    private readonly IAssessmentReviewRepository _reviewRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IAssessmentGenerationAgent _generationAgent;
    private readonly IAssessmentReviewAgent _reviewAgent;
    private readonly GeneratedAssessmentValidator _validator;

    public SemanticKernelAssessmentWorkflowOrchestrator(
        ISpecificationDraftRepository specificationRepository,
        IGeneratedAssessmentRepository assessmentRepository,
        IAssessmentReviewRepository reviewRepository,
        IAuditLogRepository auditLogRepository,
        IAssessmentGenerationAgent generationAgent,
        IAssessmentReviewAgent reviewAgent,
        GeneratedAssessmentValidator validator)
    {
        _specificationRepository = specificationRepository;
        _assessmentRepository = assessmentRepository;
        _reviewRepository = reviewRepository;
        _auditLogRepository = auditLogRepository;
        _generationAgent = generationAgent;
        _reviewAgent = reviewAgent;
        _validator = validator;
    }

    public async Task<AssessmentWorkflowResult> GenerateAndReviewAsync(
        string specificationId,
        string specificationVersion,
        string requestedBy,
        CancellationToken cancellationToken)
    {
        var specification = await _specificationRepository.GetAsync(specificationId, specificationVersion, cancellationToken);
        if (specification is null)
        {
            return AssessmentWorkflowResult.NotFound();
        }

        if (!string.Equals(specification.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            return AssessmentWorkflowResult.BadRequest("Specification version must be approved before generating an assessment.");
        }

        await AddAuditEntryAsync(
            "semantic-kernel.workflow.started",
            "specification",
            $"{specification.Id}:{specification.Version}",
            "Semantic Kernel assessment workflow started.",
            requestedBy,
            cancellationToken);

        AssessmentGenerationAgentResult agentResult;
        try
        {
            agentResult = await _generationAgent.GenerateAsync(specification, cancellationToken);
        }
        catch (AssessmentGenerationException exception)
        {
            return AssessmentWorkflowResult.BadGateway(exception.Message);
        }

        var validationResult = _validator.Validate(specification, agentResult.Questions);
        if (!validationResult.IsValid)
        {
            return AssessmentWorkflowResult.BadGateway(
                "Azure OpenAI returned an assessment that does not match the approved specification.",
                validationResult.Errors);
        }

        var now = DateTimeOffset.UtcNow;
        var assessment = new GeneratedAssessment(
            $"assessment-{Guid.NewGuid():N}",
            specification.Id,
            specification.Version,
            "generated",
            agentResult.Questions,
            requestedBy.Trim(),
            now);

        var savedAssessment = await _assessmentRepository.CreateAsync(assessment, cancellationToken);
        await AddAuditEntryAsync(
            "assessment.generated",
            "assessment",
            savedAssessment.Id,
            $"Assessment generated for specification {specification.Id} version {specification.Version}.",
            requestedBy,
            cancellationToken);

        AssessmentReview review;
        try
        {
            review = await _reviewAgent.ReviewAsync(specification, savedAssessment, cancellationToken);
        }
        catch (AssessmentGenerationException exception)
        {
            return AssessmentWorkflowResult.BadGateway(exception.Message);
        }

        var savedReview = await _reviewRepository.CreateAsync(review, cancellationToken);
        await AddAuditEntryAsync(
            "assessment.reviewed",
            "assessment-review",
            savedReview.Id,
            $"Assessment review completed with status '{savedReview.Status}'.",
            "Assessment Review Agent",
            cancellationToken);

        await AddAuditEntryAsync(
            "semantic-kernel.workflow.completed",
            "assessment",
            savedAssessment.Id,
            "Semantic Kernel assessment workflow completed.",
            "Semantic Kernel Orchestrator",
            cancellationToken);

        return AssessmentWorkflowResult.Success(new GenerateAssessmentResponse(savedAssessment, savedReview));
    }

    private async Task AddAuditEntryAsync(
        string eventType,
        string entityType,
        string entityId,
        string message,
        string actor,
        CancellationToken cancellationToken)
    {
        var entry = new AuditLogEntry(
            $"audit-{Guid.NewGuid():N}",
            eventType,
            entityType,
            entityId,
            message,
            actor.Trim(),
            DateTimeOffset.UtcNow);

        await _auditLogRepository.CreateAsync(entry, cancellationToken);
    }
}

public sealed record GenerateAssessmentResponse(
    GeneratedAssessment Assessment,
    AssessmentReview Review);

public sealed record AssessmentWorkflowResult(
    AssessmentWorkflowStatus Status,
    GenerateAssessmentResponse? Response,
    string? Message,
    IReadOnlyList<string> Errors)
{
    public static AssessmentWorkflowResult Success(GenerateAssessmentResponse response)
    {
        return new AssessmentWorkflowResult(AssessmentWorkflowStatus.Success, response, null, []);
    }

    public static AssessmentWorkflowResult NotFound()
    {
        return new AssessmentWorkflowResult(AssessmentWorkflowStatus.NotFound, null, null, []);
    }

    public static AssessmentWorkflowResult BadRequest(string message)
    {
        return new AssessmentWorkflowResult(AssessmentWorkflowStatus.BadRequest, null, message, []);
    }

    public static AssessmentWorkflowResult BadGateway(string message, IReadOnlyList<string>? errors = null)
    {
        return new AssessmentWorkflowResult(AssessmentWorkflowStatus.BadGateway, null, message, errors ?? []);
    }
}

public enum AssessmentWorkflowStatus
{
    Success,
    NotFound,
    BadRequest,
    BadGateway
}

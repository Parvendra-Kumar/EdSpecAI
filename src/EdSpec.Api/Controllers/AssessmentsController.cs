using System.ComponentModel.DataAnnotations;
using EdSpec.Agents.Assessments;
using EdSpec.Application.Assessments;
using EdSpec.Application.Audit;
using EdSpec.Application.Specifications;
using EdSpec.Domain.Assessments;
using EdSpec.Domain.Audit;
using EdSpec.Validation.Assessments;
using Microsoft.AspNetCore.Mvc;

namespace EdSpec.Api.Controllers;

[ApiController]
[Route("api/specifications/{id}/versions/{version}/assessments")]
public sealed class AssessmentsController : ControllerBase
{
    private readonly ISpecificationDraftRepository _specificationRepository;
    private readonly IGeneratedAssessmentRepository _assessmentRepository;
    private readonly IAssessmentReviewRepository _reviewRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IAssessmentGenerationAgent _generationAgent;
    private readonly IAssessmentReviewAgent _reviewAgent;
    private readonly GeneratedAssessmentValidator _validator;

    public AssessmentsController(
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

    [HttpPost("generate", Name = "GenerateAssessment")]
    [ProducesResponseType(typeof(GenerateAssessmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<GenerateAssessmentResponse>> Generate(
        string id,
        string version,
        GenerateAssessmentRequest request,
        CancellationToken cancellationToken)
    {
        var specification = await _specificationRepository.GetAsync(id, version, cancellationToken);
        if (specification is null)
        {
            return NotFound();
        }

        if (!string.Equals(specification.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Specification version must be approved before generating an assessment." });
        }

        AssessmentGenerationAgentResult agentResult;
        try
        {
            agentResult = await _generationAgent.GenerateAsync(specification, cancellationToken);
        }
        catch (AssessmentGenerationException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }

        var validationResult = _validator.Validate(specification, agentResult.Questions);
        if (!validationResult.IsValid)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Azure OpenAI returned an assessment that does not match the approved specification.",
                errors = validationResult.Errors
            });
        }

        var now = DateTimeOffset.UtcNow;
        var assessment = new GeneratedAssessment(
            $"assessment-{Guid.NewGuid():N}",
            specification.Id,
            specification.Version,
            "generated",
            agentResult.Questions,
            request.RequestedBy.Trim(),
            now);

        var savedAssessment = await _assessmentRepository.CreateAsync(assessment, cancellationToken);
        await AddAuditEntryAsync(
            "assessment.generated",
            "assessment",
            savedAssessment.Id,
            $"Assessment generated for specification {specification.Id} version {specification.Version}.",
            request.RequestedBy,
            cancellationToken);

        AssessmentReview review;
        try
        {
            review = await _reviewAgent.ReviewAsync(specification, savedAssessment, cancellationToken);
        }
        catch (AssessmentGenerationException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }

        var savedReview = await _reviewRepository.CreateAsync(review, cancellationToken);
        await AddAuditEntryAsync(
            "assessment.reviewed",
            "assessment-review",
            savedReview.Id,
            $"Assessment review completed with status '{savedReview.Status}'.",
            "Assessment Review Agent",
            cancellationToken);

        return Ok(new GenerateAssessmentResponse(savedAssessment, savedReview));
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

public sealed record GenerateAssessmentRequest(
    [Required, MinLength(2)] string RequestedBy);

public sealed record GenerateAssessmentResponse(
    GeneratedAssessment Assessment,
    AssessmentReview Review);

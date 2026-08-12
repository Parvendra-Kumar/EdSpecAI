using System.ComponentModel.DataAnnotations;
using EdSpec.Agents.Assessments;
using EdSpec.Application.Assessments;
using EdSpec.Application.Specifications;
using EdSpec.Domain.Assessments;
using EdSpec.Validation.Assessments;
using Microsoft.AspNetCore.Mvc;

namespace EdSpec.Api.Controllers;

[ApiController]
[Route("api/specifications/{id}/versions/{version}/assessments")]
public sealed class AssessmentsController : ControllerBase
{
    private readonly ISpecificationDraftRepository _specificationRepository;
    private readonly IGeneratedAssessmentRepository _assessmentRepository;
    private readonly IAssessmentGenerationAgent _generationAgent;
    private readonly GeneratedAssessmentValidator _validator;

    public AssessmentsController(
        ISpecificationDraftRepository specificationRepository,
        IGeneratedAssessmentRepository assessmentRepository,
        IAssessmentGenerationAgent generationAgent,
        GeneratedAssessmentValidator validator)
    {
        _specificationRepository = specificationRepository;
        _assessmentRepository = assessmentRepository;
        _generationAgent = generationAgent;
        _validator = validator;
    }

    [HttpPost("generate", Name = "GenerateAssessment")]
    [ProducesResponseType(typeof(GeneratedAssessment), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<GeneratedAssessment>> Generate(
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

        return Ok(savedAssessment);
    }
}

public sealed record GenerateAssessmentRequest(
    [Required, MinLength(2)] string RequestedBy);

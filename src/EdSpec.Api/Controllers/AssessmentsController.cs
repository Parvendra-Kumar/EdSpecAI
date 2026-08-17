using System.ComponentModel.DataAnnotations;
using EdSpec.Api.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace EdSpec.Api.Controllers;

[ApiController]
[Route("api/specifications/{id}/versions/{version}/assessments")]
public sealed class AssessmentsController : ControllerBase
{
    private readonly SemanticKernelAssessmentWorkflowOrchestrator _orchestrator;

    public AssessmentsController(SemanticKernelAssessmentWorkflowOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
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
        var result = await _orchestrator.GenerateAndReviewAsync(
            id,
            version,
            request.RequestedBy,
            cancellationToken);

        return result.Status switch
        {
            AssessmentWorkflowStatus.Success => Ok(result.Response),
            AssessmentWorkflowStatus.NotFound => NotFound(),
            AssessmentWorkflowStatus.BadRequest => BadRequest(new { message = result.Message }),
            AssessmentWorkflowStatus.BadGateway => StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = result.Message,
                errors = result.Errors
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}

public sealed record GenerateAssessmentRequest(
    [Required, MinLength(2)] string RequestedBy);

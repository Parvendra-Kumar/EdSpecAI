using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using EdSpec.Application.Assessments;
using EdSpec.Api.Workflows;
using EdSpec.Domain.Assessments;
using Microsoft.AspNetCore.Mvc;

namespace EdSpec.Api.Controllers;

[ApiController]
[Route("api/specifications/{id}/versions/{version}/assessments")]
public sealed class AssessmentsController : ControllerBase
{
    private readonly SemanticKernelAssessmentWorkflowOrchestrator _orchestrator;
    private readonly IGeneratedAssessmentRepository _assessmentRepository;

    public AssessmentsController(
        SemanticKernelAssessmentWorkflowOrchestrator orchestrator,
        IGeneratedAssessmentRepository assessmentRepository)
    {
        _orchestrator = orchestrator;
        _assessmentRepository = assessmentRepository;
    }

    [HttpGet("~/api/assessments", Name = "GetAssessments")]
    [ProducesResponseType(typeof(IReadOnlyCollection<AssessmentListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AssessmentListItem>>> GetAll(
        CancellationToken cancellationToken)
    {
        var assessments = await _assessmentRepository.GetAllAsync(cancellationToken);

        return Ok(assessments
            .OrderByDescending(assessment => assessment.CreatedAt)
            .Select(AssessmentListItem.From)
            .ToList());
    }

    [HttpGet("~/api/assessments/{assessmentId}", Name = "GetAssessment")]
    [ProducesResponseType(typeof(GeneratedAssessment), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GeneratedAssessment>> Get(
        string assessmentId,
        CancellationToken cancellationToken)
    {
        var assessment = await _assessmentRepository.GetAsync(assessmentId, cancellationToken);

        return assessment is null ? NotFound(new { message = "Assessment was not found." }) : Ok(assessment);
    }

    [HttpGet("~/api/assessments/{assessmentId}/download", Name = "DownloadAssessment")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        string assessmentId,
        CancellationToken cancellationToken)
    {
        var assessment = await _assessmentRepository.GetAsync(assessmentId, cancellationToken);
        if (assessment is null)
        {
            return NotFound(new { message = "Assessment was not found." });
        }

        var html = AssessmentDownloadDocument.Create(assessment);
        return File(
            Encoding.UTF8.GetBytes(html),
            "text/html",
            $"{assessment.Id}.html");
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

public sealed record AssessmentListItem(
    string Id,
    string SpecificationId,
    string SpecificationVersion,
    string Status,
    int QuestionCount,
    int TotalPoints,
    string CreatedBy,
    DateTimeOffset CreatedAt)
{
    public static AssessmentListItem From(GeneratedAssessment assessment)
    {
        return new AssessmentListItem(
            assessment.Id,
            assessment.SpecificationId,
            assessment.SpecificationVersion,
            assessment.Status,
            assessment.Questions.Count,
            assessment.Questions.Sum(question => question.Points),
            assessment.CreatedBy,
            assessment.CreatedAt);
    }
}

internal static class AssessmentDownloadDocument
{
    public static string Create(GeneratedAssessment assessment)
    {
        var document = new StringBuilder();
        document.AppendLine("<!doctype html>");
        document.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\"><title>Assessment</title>");
        document.AppendLine("<style>body{font-family:Arial,sans-serif;max-width:900px;margin:40px auto;color:#182238}h1{margin-bottom:4px}.meta{color:#58657c;margin-bottom:30px}.question{margin:24px 0;padding:18px;border:1px solid #dce2ec;border-radius:8px}.option{margin:8px 0}.answer{margin-top:14px;color:#218c58;font-weight:bold}</style></head><body>");
        document.Append("<h1>Assessment</h1><div class=\"meta\">");
        document.Append("Specification: ").Append(Escape(assessment.SpecificationId));
        document.Append(" · Version: ").Append(Escape(assessment.SpecificationVersion));
        document.Append(" · Created by: ").Append(Escape(assessment.CreatedBy));
        document.Append(" · Created: ").Append(Escape(assessment.CreatedAt.ToString("u")));
        document.AppendLine("</div>");

        for (var index = 0; index < assessment.Questions.Count; index++)
        {
            var question = assessment.Questions[index];
            document.Append("<section class=\"question\"><h2>Question ")
                .Append(index + 1)
                .Append("</h2><p>")
                .Append(Escape(question.Prompt))
                .Append("</p>");

            foreach (var option in question.Options)
            {
                document.Append("<div class=\"option\"><strong>")
                    .Append(Escape(option.Id))
                    .Append(".</strong> ")
                    .Append(Escape(option.Text))
                    .AppendLine("</div>");
            }

            document.Append("<div class=\"answer\">Correct answer: ")
                .Append(Escape(question.CorrectOptionId))
                .Append(" · ")
                .Append(question.Points)
                .AppendLine(" points</div></section>");
        }

        document.AppendLine("</body></html>");
        return document.ToString();
    }

    private static string Escape(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}

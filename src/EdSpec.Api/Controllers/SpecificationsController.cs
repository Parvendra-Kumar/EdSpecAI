using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using EdSpec.Application.Audit;
using EdSpec.Application.Specifications;
using EdSpec.Domain.Audit;
using EdSpec.Domain.Specifications;
using EdSpec.Infrastructure.Specifications;
using Microsoft.AspNetCore.Mvc;

namespace EdSpec.Api.Controllers;

[ApiController]
[Route("api/specifications")]
public sealed partial class SpecificationsController : ControllerBase
{
    private readonly ISpecificationDraftRepository _repository;
    private readonly IAuditLogRepository _auditLogRepository;

    public SpecificationsController(ISpecificationDraftRepository repository, IAuditLogRepository auditLogRepository)
    {
        _repository = repository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpPost("drafts", Name = "CreateDraftSpecification")]
    [ProducesResponseType(typeof(SpecificationDraft), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SpecificationDraft>> CreateDraft(
        CreateDraftSpecificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidateDraftRequest(request))
        {
            return ValidationProblem(ModelState);
        }

        var now = DateTimeOffset.UtcNow;
        var specificationId = string.IsNullOrWhiteSpace(request.Id)
            ? Slugify(request.Title)
            : Slugify(request.Id);

        var draft = new SpecificationDraft(
            specificationId,
            request.Version.Trim(),
            "draft",
            request.Title.Trim(),
            request.Subject.Trim(),
            request.LearningObjective.Trim(),
            request.QuestionRules.ToDomain(),
            request.DifficultyDistribution.ToDomain(),
            request.ScoringRules.ToDomain(),
            new ApprovalInfo(true, null, null),
            now,
            now);

        try
        {
            var savedDraft = await _repository.CreateAsync(draft, cancellationToken);
            return CreatedAtRoute("GetSpecificationDraft", new { id = savedDraft.Id, version = savedDraft.Version }, savedDraft);
        }
        catch (DuplicateSpecificationVersionException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpGet("{id}/versions/{version}", Name = "GetSpecificationDraft")]
    [ProducesResponseType(typeof(SpecificationDraft), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpecificationDraft>> GetDraft(
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        var draft = await _repository.GetAsync(id, version, cancellationToken);

        return draft is null ? NotFound() : Ok(draft);
    }

    [HttpPut("{id}/versions/{version}", Name = "UpdateSpecificationDraft")]
    [ProducesResponseType(typeof(SpecificationDraft), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpecificationDraft>> UpdateDraft(
        string id,
        string version,
        UpdateDraftSpecificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidateDraftRequest(request))
        {
            return ValidationProblem(ModelState);
        }

        var existingDraft = await _repository.GetAsync(id, version, cancellationToken);
        if (existingDraft is null)
        {
            return NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var draft = new SpecificationDraft(
            existingDraft.Id,
            existingDraft.Version,
            "draft",
            request.Title.Trim(),
            request.Subject.Trim(),
            request.LearningObjective.Trim(),
            request.QuestionRules.ToDomain(),
            request.DifficultyDistribution.ToDomain(),
            request.ScoringRules.ToDomain(),
            existingDraft.Approval,
            existingDraft.CreatedAt,
            now);

        var savedDraft = await _repository.UpdateAsync(draft, cancellationToken);

        return Ok(savedDraft);
    }

    [HttpPost("{id}/versions/{version}/approve", Name = "ApproveSpecificationDraft")]
    [ProducesResponseType(typeof(SpecificationDraft), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpecificationDraft>> ApproveDraft(
        string id,
        string version,
        ApproveSpecificationRequest request,
        CancellationToken cancellationToken)
    {
        var existingDraft = await _repository.GetAsync(id, version, cancellationToken);
        if (existingDraft is null)
        {
            return NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var approvedDraft = existingDraft with
        {
            Status = "approved",
            Approval = new ApprovalInfo(true, request.ApprovedBy.Trim(), now),
            UpdatedAt = now
        };

        var savedDraft = await _repository.UpdateAsync(approvedDraft, cancellationToken);
        await _auditLogRepository.CreateAsync(
            new AuditLogEntry(
                $"audit-{Guid.NewGuid():N}",
                "specification.approved",
                "specification",
                $"{savedDraft.Id}:{savedDraft.Version}",
                $"Specification {savedDraft.Id} version {savedDraft.Version} approved.",
                request.ApprovedBy.Trim(),
                now),
            cancellationToken);

        return Ok(savedDraft);
    }

    private bool ValidateDraftRequest(DraftSpecificationRequest request)
    {
        if (request.DifficultyDistribution.TotalQuestions != request.QuestionRules.TotalQuestions)
        {
            ModelState.AddModelError(
                nameof(request.DifficultyDistribution),
                "Difficulty distribution must add up to the total question count.");
        }

        if (request.ScoringRules.TotalPoints != request.QuestionRules.TotalQuestions * request.ScoringRules.PointsPerQuestion)
        {
            ModelState.AddModelError(
                nameof(request.ScoringRules),
                "Total points must equal total questions multiplied by points per question.");
        }

        return ModelState.IsValid;
    }

    private static string Slugify(string value)
    {
        var slug = NonAlphanumericCharacters()
            .Replace(value.Trim().ToLowerInvariant(), "-")
            .Trim('-');

        return string.IsNullOrWhiteSpace(slug) ? $"spec-{Guid.NewGuid():N}" : slug;
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonAlphanumericCharacters();
}

public abstract record DraftSpecificationRequest(
    [Required, MinLength(3)] string Title,
    [Required, MinLength(3)] string Subject,
    [Required, MinLength(10)] string LearningObjective,
    [Required] QuestionRulesRequest QuestionRules,
    [Required] DifficultyDistributionRequest DifficultyDistribution,
    [Required] ScoringRulesRequest ScoringRules);

public sealed record CreateDraftSpecificationRequest(
    string? Id,
    [Required, RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Version must use semantic version format, for example 1.0.0.")]
    string Version,
    [Required, MinLength(3)] string Title,
    [Required, MinLength(3)] string Subject,
    [Required, MinLength(10)] string LearningObjective,
    [Required] QuestionRulesRequest QuestionRules,
    [Required] DifficultyDistributionRequest DifficultyDistribution,
    [Required] ScoringRulesRequest ScoringRules)
    : DraftSpecificationRequest(Title, Subject, LearningObjective, QuestionRules, DifficultyDistribution, ScoringRules);

public sealed record UpdateDraftSpecificationRequest(
    [Required, MinLength(3)] string Title,
    [Required, MinLength(3)] string Subject,
    [Required, MinLength(10)] string LearningObjective,
    [Required] QuestionRulesRequest QuestionRules,
    [Required] DifficultyDistributionRequest DifficultyDistribution,
    [Required] ScoringRulesRequest ScoringRules)
    : DraftSpecificationRequest(Title, Subject, LearningObjective, QuestionRules, DifficultyDistribution, ScoringRules);

public sealed record ApproveSpecificationRequest(
    [Required, MinLength(2)] string ApprovedBy);

public sealed record QuestionRulesRequest(
    [Range(1, 100)] int TotalQuestions,
    [Required, MinLength(3)] string QuestionType,
    [Range(2, 8)] int OptionsPerQuestion)
{
    public QuestionRules ToDomain()
    {
        return new QuestionRules(TotalQuestions, QuestionType.Trim(), OptionsPerQuestion);
    }
}

public sealed record DifficultyDistributionRequest(
    [Range(0, 100)] int Easy,
    [Range(0, 100)] int Medium,
    [Range(0, 100)] int Hard)
{
    public int TotalQuestions => Easy + Medium + Hard;

    public DifficultyDistribution ToDomain()
    {
        return new DifficultyDistribution(Easy, Medium, Hard);
    }
}

public sealed record ScoringRulesRequest(
    [Range(1, 100)] int PointsPerQuestion,
    [Range(1, 10000)] int TotalPoints)
{
    public ScoringRules ToDomain()
    {
        return new ScoringRules(PointsPerQuestion, TotalPoints);
    }
}

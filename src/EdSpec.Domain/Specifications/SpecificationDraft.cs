namespace EdSpec.Domain.Specifications;

public sealed record SpecificationDraft(
    string Id,
    string Version,
    string Status,
    string Title,
    string Subject,
    string LearningObjective,
    QuestionRules QuestionRules,
    DifficultyDistribution DifficultyDistribution,
    ScoringRules ScoringRules,
    ApprovalInfo Approval,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record QuestionRules(
    int TotalQuestions,
    string QuestionType,
    int OptionsPerQuestion);

public sealed record DifficultyDistribution(
    int Easy,
    int Medium,
    int Hard)
{
    public int TotalQuestions => Easy + Medium + Hard;
}

public sealed record ScoringRules(
    int PointsPerQuestion,
    int TotalPoints);

public sealed record ApprovalInfo(
    bool Required,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt);

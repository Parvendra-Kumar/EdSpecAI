namespace EdSpec.Domain.Assessments;

public sealed record GeneratedAssessment(
    string Id,
    string SpecificationId,
    string SpecificationVersion,
    string Status,
    IReadOnlyList<GeneratedQuestion> Questions,
    string CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record GeneratedQuestion(
    string Id,
    string LearningObjective,
    string Difficulty,
    string QuestionType,
    string Prompt,
    IReadOnlyList<GeneratedOption> Options,
    string CorrectOptionId,
    int Points);

public sealed record GeneratedOption(
    string Id,
    string Text);

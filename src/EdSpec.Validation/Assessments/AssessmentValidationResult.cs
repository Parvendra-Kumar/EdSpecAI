namespace EdSpec.Validation.Assessments;

public sealed record AssessmentValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static AssessmentValidationResult Success { get; } = new([]);
}

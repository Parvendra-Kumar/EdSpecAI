namespace EdSpec.Domain.Assessments;

public sealed record AssessmentReview(
    string Id,
    string AssessmentId,
    string SpecificationId,
    string SpecificationVersion,
    string Status,
    IReadOnlyList<AssessmentReviewFinding> Findings,
    decimal Confidence,
    DateTimeOffset CreatedAt);

public sealed record AssessmentReviewFinding(
    string Severity,
    string Message,
    string Evidence,
    decimal Confidence);

using EdSpec.Domain.Assessments;
using EdSpec.Domain.Specifications;
using EdSpec.Validation.Assessments;

namespace EdSpec.UnitTests.Assessments;

public sealed class GeneratedAssessmentValidatorTests
{
    private readonly GeneratedAssessmentValidator _validator = new();

    [Fact]
    public void Validate_ReturnsSuccess_WhenAssessmentMatchesSpecification()
    {
        var result = _validator.Validate(CreateSpecification(), CreateQuestions());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsError_WhenQuestionCountDoesNotMatchSpecification()
    {
        var result = _validator.Validate(CreateSpecification(), CreateQuestions().Take(4).ToList());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("question count", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenOptionCountDoesNotMatchSpecification()
    {
        var questions = CreateQuestions();
        questions[0] = questions[0] with
        {
            Options = questions[0].Options.Take(3).ToList()
        };

        var result = _validator.Validate(CreateSpecification(), questions);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("options", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenDifficultyDistributionDoesNotMatchSpecification()
    {
        var questions = CreateQuestions();
        questions[0] = questions[0] with { Difficulty = "hard" };

        var result = _validator.Validate(CreateSpecification(), questions);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Difficulty 'easy'", StringComparison.OrdinalIgnoreCase));
    }

    private static SpecificationDraft CreateSpecification()
    {
        return new SpecificationDraft(
            "algebra-basic",
            "1.0.0",
            "approved",
            "Basic Algebra Assessment",
            "Basic Algebra",
            "Solve single-variable linear equations",
            new QuestionRules(5, "multiple-choice", 4),
            new DifficultyDistribution(2, 2, 1),
            new ScoringRules(2, 10),
            new ApprovalInfo(true, "Reviewer", DateTimeOffset.UtcNow),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static List<GeneratedQuestion> CreateQuestions()
    {
        return
        [
            CreateQuestion("q1", "easy"),
            CreateQuestion("q2", "easy"),
            CreateQuestion("q3", "medium"),
            CreateQuestion("q4", "medium"),
            CreateQuestion("q5", "hard")
        ];
    }

    private static GeneratedQuestion CreateQuestion(string id, string difficulty)
    {
        return new GeneratedQuestion(
            id,
            "Solve single-variable linear equations",
            difficulty,
            "multiple-choice",
            "Solve x + 2 = 5.",
            [
                new GeneratedOption("A", "3"),
                new GeneratedOption("B", "4"),
                new GeneratedOption("C", "5"),
                new GeneratedOption("D", "6")
            ],
            "A",
            2);
    }
}

using EdSpec.Domain.Assessments;
using EdSpec.Domain.Specifications;

namespace EdSpec.Validation.Assessments;

public sealed class GeneratedAssessmentValidator
{
    public AssessmentValidationResult Validate(SpecificationDraft specification, IReadOnlyList<GeneratedQuestion> questions)
    {
        var errors = new List<string>();

        if (questions.Count != specification.QuestionRules.TotalQuestions)
        {
            errors.Add("Generated question count must match specification total questions.");
        }

        foreach (var question in questions)
        {
            if (!string.Equals(question.QuestionType, specification.QuestionRules.QuestionType, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Question '{question.Id}' must use question type '{specification.QuestionRules.QuestionType}'.");
            }

            if (question.Options.Count != specification.QuestionRules.OptionsPerQuestion)
            {
                errors.Add($"Question '{question.Id}' must have exactly {specification.QuestionRules.OptionsPerQuestion} options.");
            }

            if (question.Options.Count(option => string.Equals(option.Id, question.CorrectOptionId, StringComparison.OrdinalIgnoreCase)) != 1)
            {
                errors.Add($"Question '{question.Id}' must have exactly one correct option.");
            }

            if (question.Points != specification.ScoringRules.PointsPerQuestion)
            {
                errors.Add($"Question '{question.Id}' must be worth {specification.ScoringRules.PointsPerQuestion} points.");
            }
        }

        AddDifficultyError(errors, questions, "easy", specification.DifficultyDistribution.Easy);
        AddDifficultyError(errors, questions, "medium", specification.DifficultyDistribution.Medium);
        AddDifficultyError(errors, questions, "hard", specification.DifficultyDistribution.Hard);

        return errors.Count == 0 ? AssessmentValidationResult.Success : new AssessmentValidationResult(errors);
    }

    private static void AddDifficultyError(List<string> errors, IReadOnlyList<GeneratedQuestion> questions, string difficulty, int expectedCount)
    {
        var actualCount = questions.Count(question => string.Equals(question.Difficulty, difficulty, StringComparison.OrdinalIgnoreCase));
        if (actualCount != expectedCount)
        {
            errors.Add($"Difficulty '{difficulty}' must have {expectedCount} questions.");
        }
    }
}

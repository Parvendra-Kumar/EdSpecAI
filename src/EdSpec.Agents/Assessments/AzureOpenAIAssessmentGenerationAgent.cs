using System.Text.Json;
using EdSpec.Application.Assessments;
using EdSpec.Domain.Assessments;
using EdSpec.Domain.Specifications;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace EdSpec.Agents.Assessments;

public sealed class AzureOpenAIAssessmentGenerationAgent : IAssessmentGenerationAgent
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly Kernel _kernel;
    private readonly string _promptPath;

    public AzureOpenAIAssessmentGenerationAgent(
        Kernel kernel,
        IHostEnvironment environment)
    {
        _kernel = kernel;
        _promptPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "prompts", "creation-v1.md"));
    }

    public async Task<AssessmentGenerationAgentResult> GenerateAsync(
        SpecificationDraft specification,
        CancellationToken cancellationToken)
    {
        var promptRules = await File.ReadAllTextAsync(_promptPath, cancellationToken);
        var content = await GetSemanticKernelResponseAsync(promptRules, specification, cancellationToken);
        var agentOutput = JsonSerializer.Deserialize<AssessmentAgentOutput>(content, SerializerOptions);
        if (agentOutput?.Questions is null)
        {
            throw new AssessmentGenerationException("Azure OpenAI returned invalid assessment JSON.");
        }

        return new AssessmentGenerationAgentResult(agentOutput.Questions);
    }

    private async Task<string> GetSemanticKernelResponseAsync(
        string promptRules,
        SpecificationDraft specification,
        CancellationToken cancellationToken)
    {
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage($"{promptRules}\nReturn JSON only using this shape: {{ \"questions\": [{{ \"id\": \"q1\", \"learningObjective\": \"...\", \"difficulty\": \"easy|medium|hard\", \"questionType\": \"multiple-choice\", \"prompt\": \"...\", \"options\": [{{ \"id\": \"A\", \"text\": \"...\" }}], \"correctOptionId\": \"A\", \"points\": 2 }}] }}");
        history.AddUserMessage(JsonSerializer.Serialize(new
        {
            specification.Id,
            specification.Version,
            specification.Title,
            specification.Subject,
            specification.LearningObjective,
            specification.QuestionRules,
            specification.DifficultyDistribution,
            specification.ScoringRules
        }, SerializerOptions));

        var settings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = "json_object"
        };

        var response = await chatCompletion.GetChatMessageContentAsync(history, settings, _kernel, cancellationToken);
        var content = response.Content;

        return string.IsNullOrWhiteSpace(content)
            ? throw new AssessmentGenerationException("Azure OpenAI returned empty assessment content.")
            : content;
    }

    private sealed record AssessmentAgentOutput(IReadOnlyList<GeneratedQuestion> Questions);
}

public sealed class AssessmentGenerationException(string message) : InvalidOperationException(message);

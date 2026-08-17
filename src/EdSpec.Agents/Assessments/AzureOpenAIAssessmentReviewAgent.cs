using System.Text.Json;
using EdSpec.Application.Assessments;
using EdSpec.Domain.Assessments;
using EdSpec.Domain.Specifications;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace EdSpec.Agents.Assessments;

public sealed class AzureOpenAIAssessmentReviewAgent : IAssessmentReviewAgent
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly Kernel _kernel;
    private readonly string _promptPath;

    public AzureOpenAIAssessmentReviewAgent(
        Kernel kernel,
        IHostEnvironment environment)
    {
        _kernel = kernel;
        _promptPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "prompts", "review-v1.md"));
    }

    public async Task<AssessmentReview> ReviewAsync(
        SpecificationDraft specification,
        GeneratedAssessment assessment,
        CancellationToken cancellationToken)
    {
        var promptRules = await File.ReadAllTextAsync(_promptPath, cancellationToken);
        var content = await GetSemanticKernelResponseAsync(promptRules, specification, assessment, cancellationToken);
        var agentOutput = JsonSerializer.Deserialize<AssessmentReviewAgentOutput>(content, SerializerOptions);
        if (agentOutput is null)
        {
            throw new AssessmentGenerationException("Azure OpenAI returned invalid assessment review JSON.");
        }

        return new AssessmentReview(
            $"review-{Guid.NewGuid():N}",
            assessment.Id,
            specification.Id,
            specification.Version,
            string.IsNullOrWhiteSpace(agentOutput.Status) ? "needs_revision" : agentOutput.Status.Trim(),
            agentOutput.Findings ?? [],
            agentOutput.Confidence,
            DateTimeOffset.UtcNow);
    }

    private async Task<string> GetSemanticKernelResponseAsync(
        string promptRules,
        SpecificationDraft specification,
        GeneratedAssessment assessment,
        CancellationToken cancellationToken)
    {
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage($"{promptRules}\nReturn JSON only using this shape: {{ \"status\": \"passed|needs_revision\", \"confidence\": 0.85, \"findings\": [{{ \"severity\": \"low|medium|high\", \"message\": \"...\", \"evidence\": \"...\", \"confidence\": 0.9 }}] }}");
        history.AddUserMessage(JsonSerializer.Serialize(new
        {
            Specification = specification,
            Assessment = assessment
        }, SerializerOptions));

        var settings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = "json_object"
        };

        var response = await chatCompletion.GetChatMessageContentAsync(history, settings, _kernel, cancellationToken);
        var content = response.Content;

        return string.IsNullOrWhiteSpace(content)
            ? throw new AssessmentGenerationException("Azure OpenAI returned empty assessment review content.")
            : content;
    }

    private sealed record AssessmentReviewAgentOutput(
        string Status,
        IReadOnlyList<AssessmentReviewFinding>? Findings,
        decimal Confidence);
}

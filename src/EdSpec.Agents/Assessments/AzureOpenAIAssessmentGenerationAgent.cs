using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EdSpec.Application.Assessments;
using EdSpec.Domain.Assessments;
using EdSpec.Domain.Specifications;
using Microsoft.Extensions.Configuration;

namespace EdSpec.Agents.Assessments;

public sealed class AzureOpenAIAssessmentGenerationAgent : IAssessmentGenerationAgent
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly string _promptPath;

    public AzureOpenAIAssessmentGenerationAgent(
        HttpClient httpClient,
        IConfiguration configuration,
        Microsoft.Extensions.Hosting.IHostEnvironment environment)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _promptPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "prompts", "creation-v1.md"));
    }

    public async Task<AssessmentGenerationAgentResult> GenerateAsync(
        SpecificationDraft specification,
        CancellationToken cancellationToken)
    {
        var endpoint = GetRequiredSetting("AzureOpenAI:Endpoint");
        var apiKey = GetRequiredSetting("AzureOpenAI:ApiKey");
        var deploymentName = GetRequiredSetting("AzureOpenAI:DeploymentName");
        var promptRules = await File.ReadAllTextAsync(_promptPath, cancellationToken);

        var requestUri = $"{endpoint.TrimEnd('/')}/openai/deployments/{deploymentName}/chat/completions?api-version=2024-02-15-preview";
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Add("api-key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent(CreateRequestBody(promptRules, specification));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AssessmentGenerationException($"Azure OpenAI returned {(int)response.StatusCode}: {responseText}");
        }

        var content = ExtractMessageContent(responseText);
        var agentOutput = JsonSerializer.Deserialize<AssessmentAgentOutput>(content, SerializerOptions);
        if (agentOutput?.Questions is null)
        {
            throw new AssessmentGenerationException("Azure OpenAI returned invalid assessment JSON.");
        }

        return new AssessmentGenerationAgentResult(agentOutput.Questions);
    }

    private string GetRequiredSetting(string key)
    {
        return _configuration[key] ?? throw new AssessmentGenerationException($"Missing configuration value '{key}'.");
    }

    private static object CreateRequestBody(string promptRules, SpecificationDraft specification)
    {
        return new
        {
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = $"{promptRules}\nReturn JSON only using this shape: {{ \"questions\": [{{ \"id\": \"q1\", \"learningObjective\": \"...\", \"difficulty\": \"easy|medium|hard\", \"questionType\": \"multiple-choice\", \"prompt\": \"...\", \"options\": [{{ \"id\": \"A\", \"text\": \"...\" }}], \"correctOptionId\": \"A\", \"points\": 2 }}] }}"
                },
                new
                {
                    role = "user",
                    content = JsonSerializer.Serialize(new
                    {
                        specification.Id,
                        specification.Version,
                        specification.Title,
                        specification.Subject,
                        specification.LearningObjective,
                        specification.QuestionRules,
                        specification.DifficultyDistribution,
                        specification.ScoringRules
                    }, SerializerOptions)
                }
            },
            response_format = new { type = "json_object" }
        };
    }

    private static StringContent JsonContent(object value)
    {
        return new StringContent(JsonSerializer.Serialize(value, SerializerOptions), Encoding.UTF8, "application/json");
    }

    private static string ExtractMessageContent(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return string.IsNullOrWhiteSpace(content)
            ? throw new AssessmentGenerationException("Azure OpenAI returned empty assessment content.")
            : content;
    }

    private sealed record AssessmentAgentOutput(IReadOnlyList<GeneratedQuestion> Questions);
}

public sealed class AssessmentGenerationException(string message) : InvalidOperationException(message);

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EdSpec.Application.Assessments;
using EdSpec.Domain.Assessments;
using EdSpec.Domain.Specifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace EdSpec.Agents.Assessments;

public sealed class AzureOpenAIAssessmentReviewAgent : IAssessmentReviewAgent
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly string _promptPath;

    public AzureOpenAIAssessmentReviewAgent(
        HttpClient httpClient,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _promptPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "prompts", "review-v1.md"));
    }

    public async Task<AssessmentReview> ReviewAsync(
        SpecificationDraft specification,
        GeneratedAssessment assessment,
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
        request.Content = JsonContent(CreateRequestBody(promptRules, specification, assessment));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AssessmentGenerationException($"Azure OpenAI review returned {(int)response.StatusCode}: {responseText}");
        }

        var content = ExtractMessageContent(responseText);
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

    private string GetRequiredSetting(string key)
    {
        return _configuration[key] ?? throw new AssessmentGenerationException($"Missing configuration value '{key}'.");
    }

    private static object CreateRequestBody(string promptRules, SpecificationDraft specification, GeneratedAssessment assessment)
    {
        return new
        {
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = $"{promptRules}\nReturn JSON only using this shape: {{ \"status\": \"passed|needs_revision\", \"confidence\": 0.85, \"findings\": [{{ \"severity\": \"low|medium|high\", \"message\": \"...\", \"evidence\": \"...\", \"confidence\": 0.9 }}] }}"
                },
                new
                {
                    role = "user",
                    content = JsonSerializer.Serialize(new
                    {
                        Specification = specification,
                        Assessment = assessment
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
            ? throw new AssessmentGenerationException("Azure OpenAI returned empty assessment review content.")
            : content;
    }

    private sealed record AssessmentReviewAgentOutput(
        string Status,
        IReadOnlyList<AssessmentReviewFinding>? Findings,
        decimal Confidence);
}

using EdSpec.Agents.Assessments;
using EdSpec.Api.Workflows;
using EdSpec.Application.Assessments;
using EdSpec.Application.Audit;
using EdSpec.Application.Specifications;
using EdSpec.Infrastructure.Assessments;
using EdSpec.Infrastructure.Audit;
using EdSpec.Infrastructure.Specifications;
using EdSpec.Validation.Assessments;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSingleton<ISpecificationDraftRepository>(_ =>
    new JsonSpecificationDraftRepository(builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IGeneratedAssessmentRepository>(_ =>
    new JsonGeneratedAssessmentRepository(builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IAssessmentReviewRepository>(_ =>
    new JsonAssessmentReviewRepository(builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IAuditLogRepository>(_ =>
    new JsonAuditLogRepository(builder.Environment.ContentRootPath));
builder.Services.AddSingleton<GeneratedAssessmentValidator>();
var azureOpenAiEndpoint = builder.Configuration["AzureOpenAI:Endpoint"];
var azureOpenAiApiKey = builder.Configuration["AzureOpenAI:ApiKey"];
var azureOpenAiDeploymentName = builder.Configuration["AzureOpenAI:DeploymentName"];
var azureOpenAiIsConfigured = !string.IsNullOrWhiteSpace(azureOpenAiEndpoint)
    && !string.IsNullOrWhiteSpace(azureOpenAiApiKey)
    && !string.IsNullOrWhiteSpace(azureOpenAiDeploymentName);

builder.Services.AddSingleton(_ =>
{
    if (!azureOpenAiIsConfigured)
    {
        // Keep the API available for specification workflows. The assessment workflow
        // reports a 502 with setup guidance when it tries to use this empty kernel.
        return Kernel.CreateBuilder().Build();
    }

    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddAzureOpenAIChatCompletion(azureOpenAiDeploymentName!, azureOpenAiEndpoint!, azureOpenAiApiKey!);

    return kernelBuilder.Build();
});
builder.Services.AddTransient<IAssessmentGenerationAgent, AzureOpenAIAssessmentGenerationAgent>();
builder.Services.AddTransient<IAssessmentReviewAgent, AzureOpenAIAssessmentReviewAgent>();
builder.Services.AddTransient<SemanticKernelAssessmentWorkflowOrchestrator>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// The local Vite POC runs against the HTTP development profile on port 5246.
// Production deployments should enforce HTTPS at the hosting layer.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

app.Run();

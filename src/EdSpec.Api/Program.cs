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
builder.Services.AddSingleton(_ =>
{
    var endpoint = builder.Configuration["AzureOpenAI:Endpoint"];
    var apiKey = builder.Configuration["AzureOpenAI:ApiKey"];
    var deploymentName = builder.Configuration["AzureOpenAI:DeploymentName"];

    if (string.IsNullOrWhiteSpace(endpoint)
        || string.IsNullOrWhiteSpace(apiKey)
        || string.IsNullOrWhiteSpace(deploymentName))
    {
        throw new InvalidOperationException("AzureOpenAI:Endpoint, AzureOpenAI:ApiKey, and AzureOpenAI:DeploymentName are required.");
    }

    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);

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

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

using EdSpec.Agents.Assessments;
using EdSpec.Application.Assessments;
using EdSpec.Application.Audit;
using EdSpec.Application.Specifications;
using EdSpec.Infrastructure.Assessments;
using EdSpec.Infrastructure.Audit;
using EdSpec.Infrastructure.Specifications;
using EdSpec.Validation.Assessments;

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
builder.Services.AddHttpClient<IAssessmentGenerationAgent, AzureOpenAIAssessmentGenerationAgent>();
builder.Services.AddHttpClient<IAssessmentReviewAgent, AzureOpenAIAssessmentReviewAgent>();
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

using EdSpec.Agents.Assessments;
using EdSpec.Application.Assessments;
using EdSpec.Application.Specifications;
using EdSpec.Infrastructure.Assessments;
using EdSpec.Infrastructure.Specifications;
using EdSpec.Validation.Assessments;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSingleton<ISpecificationDraftRepository>(_ =>
    new JsonSpecificationDraftRepository(builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IGeneratedAssessmentRepository>(_ =>
    new JsonGeneratedAssessmentRepository(builder.Environment.ContentRootPath));
builder.Services.AddSingleton<GeneratedAssessmentValidator>();
builder.Services.AddHttpClient<IAssessmentGenerationAgent, AzureOpenAIAssessmentGenerationAgent>();
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

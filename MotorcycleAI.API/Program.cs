using MotorcycleAI.API;
using MotorcycleAI.Core.Interfaces;
using MotorcycleAI.Core.Services;
using MotorcycleAI.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ISearchAnalyzer, SearchAnalyzer>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

public partial class Program
{
}

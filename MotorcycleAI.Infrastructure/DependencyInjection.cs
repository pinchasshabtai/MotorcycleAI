using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MotorcycleAI.Core.Interfaces;
using MotorcycleAI.Infrastructure.Database;
using MotorcycleAI.Infrastructure.LlamaCpp;
using MotorcycleAI.Infrastructure.Ollama;
using MotorcycleAI.Infrastructure.Repositories;

namespace MotorcycleAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Database:ConnectionString is required.")
            .ValidateOnStart();

        if (configuration.GetSection(LlamaCppOptions.SectionName).Exists())
        {
            services
                .AddOptions<LlamaCppOptions>()
                .Bind(configuration.GetSection(LlamaCppOptions.SectionName))
                .Validate(
                    options =>
                        Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri)
                        && (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps),
                    "LlamaCpp:BaseUrl must be an absolute HTTP or HTTPS URL.")
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.Model),
                    "LlamaCpp:Model is required.")
                .Validate(
                    options => options.TimeoutSeconds > 0,
                    "LlamaCpp:TimeoutSeconds must be greater than zero.")
                .ValidateOnStart();

            services.AddHttpClient<IStructuredResponseGenerator, LlamaCppService>((serviceProvider, httpClient) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<LlamaCppOptions>>().Value;
                httpClient.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
                httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });
        }
        else
        {
            services
                .AddOptions<OllamaOptions>()
                .Bind(configuration.GetSection(OllamaOptions.SectionName))
                .Validate(
                    options =>
                        Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri)
                        && (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps),
                    "Ollama:BaseUrl must be an absolute HTTP or HTTPS URL.")
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.Model),
                    "Ollama:Model is required.")
                .Validate(
                    options => options.TimeoutSeconds > 0,
                    "Ollama:TimeoutSeconds must be greater than zero.")
                .ValidateOnStart();

            services.AddHttpClient<IStructuredResponseGenerator, OllamaService>((serviceProvider, httpClient) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<OllamaOptions>>().Value;
                httpClient.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
                httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });
        }
        services.AddScoped<IMotorcycleRepository, MotorcycleRepository>();

        return services;
    }
}

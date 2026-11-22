namespace MotorcycleAI.Infrastructure.LlamaCpp;

public sealed class LlamaCppOptions
{
    public const string SectionName = "LlamaCpp";

    public string BaseUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; }
}

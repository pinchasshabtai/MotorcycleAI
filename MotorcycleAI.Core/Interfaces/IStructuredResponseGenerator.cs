using System.Text.Json;

namespace MotorcycleAI.Core.Interfaces;

public interface IStructuredResponseGenerator
{
    Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        JsonElement jsonSchema,
        CancellationToken cancellationToken = default);
}

using System.Text.Json;
using MotorcycleAI.Core.Models;

namespace MotorcycleAI.Core.Interfaces;

public interface IStructuredResponseGenerator
{
    Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        JsonElement jsonSchema,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelToolCall>> GenerateToolCallsAsync(
        string systemPrompt,
        string userPrompt,
        JsonElement tools,
        CancellationToken cancellationToken = default);
}

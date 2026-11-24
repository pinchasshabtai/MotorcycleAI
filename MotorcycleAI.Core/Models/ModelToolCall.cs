using System.Text.Json;

namespace MotorcycleAI.Core.Models;

public sealed record ModelToolCall(string Name, JsonElement Arguments);

namespace MotorcycleAI.Core.Models;

public sealed record MotorcycleSearchResult(
    MotorcycleSearchRequest SearchRequest,
    IReadOnlyList<Motorcycle> Motorcycles);

namespace MotorcycleAI.API.Contracts.Responses;

public sealed class MotorcycleRecommendationDto
{
    public int MotorcycleId { get; init; }

    public decimal MatchScore { get; init; }

    public string Reason { get; init; } = string.Empty;

    public IReadOnlyList<string> Advantages { get; init; } = [];

    public IReadOnlyList<string> Disadvantages { get; init; } = [];
}

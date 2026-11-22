namespace MotorcycleAI.API.Contracts.Responses;

public sealed class MotorcycleRecommendationDto
{
    public int MotorcycleId { get; init; }

    public string RankLabel { get; init; } = string.Empty;

    public decimal MatchScore { get; init; }

    public MotorcycleDetailsDto Motorcycle { get; init; } = new();

    public string Reason { get; init; } = string.Empty;

    public IReadOnlyList<string> Advantages { get; init; } = [];

    public IReadOnlyList<string> Disadvantages { get; init; } = [];
}

namespace MotorcycleAI.API.Contracts.Responses;

public sealed class MotorcycleSearchResponseDto
{
    public MotorcycleSearchRequestDto SearchRequest { get; init; } = new();

    public IReadOnlyList<MotorcycleRecommendationDto> Recommendations { get; init; } = [];

    public string Summary { get; init; } = string.Empty;
}

namespace MotorcycleAI.Core.Models;

public sealed class RecommendationResult
{
    public IReadOnlyList<Recommendation> Recommendations { get; set; } = [];

    public string Summary { get; set; } = string.Empty;
}

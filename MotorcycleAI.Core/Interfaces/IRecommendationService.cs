using MotorcycleAI.Core.Models;

namespace MotorcycleAI.Core.Interfaces;

public interface IRecommendationService
{
    Task<RecommendationResult> RecommendAsync(
        string originalQuery,
        MotorcycleSearchRequest searchRequest,
        IReadOnlyList<Motorcycle> motorcycles,
        CancellationToken cancellationToken = default);
}

using MotorcycleAI.Core.Models;

namespace MotorcycleAI.Core.Interfaces;

public interface ISearchAnalyzer
{
    Task<MotorcycleSearchRequest> AnalyzeAsync(
        string query,
        CancellationToken cancellationToken = default);
}

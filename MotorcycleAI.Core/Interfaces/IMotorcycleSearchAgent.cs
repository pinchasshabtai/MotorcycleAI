using MotorcycleAI.Core.Models;

namespace MotorcycleAI.Core.Interfaces;

public interface IMotorcycleSearchAgent
{
    Task<MotorcycleSearchResult> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}

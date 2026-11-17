using MotorcycleAI.Core.Models;

namespace MotorcycleAI.Core.Interfaces;

public interface IMotorcycleRepository
{
    Task<IReadOnlyList<Motorcycle>> SearchAsync(
        MotorcycleSearchRequest searchRequest,
        CancellationToken cancellationToken = default);

    Task<Motorcycle?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);
}

using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using MotorcycleAI.Core.Interfaces;
using MotorcycleAI.Core.Models;
using MotorcycleAI.Infrastructure.Database;

namespace MotorcycleAI.Infrastructure.Repositories;

public sealed class MotorcycleRepository : IMotorcycleRepository
{
    private readonly DatabaseOptions _options;

    public MotorcycleRepository(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IReadOnlyList<Motorcycle>> SearchAsync(
        MotorcycleSearchRequest searchRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searchRequest);

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = MotorcycleSearchQueryBuilder.Build(searchRequest, cancellationToken);
        var motorcycles = await connection.QueryAsync<Motorcycle>(command);
        return motorcycles.AsList();
    }

    public async Task<Motorcycle?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id,
                Manufacturer,
                Model,
                [Year] AS [Year],
                EngineCC,
                Price,
                PriceIsEstimate,
                AttributesAreEstimated,
                VehicleType,
                MotorcycleType,
                RiderExperience,
                [Usage] AS [Usage],
                Reliability,
                MaintenanceCost,
                Description,
                ImageUrl
            FROM dbo.Motorcycles
            WHERE Id = @Id;
            """;

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = new CommandDefinition(
            sql,
            new { Id = id },
            cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Motorcycle>(command);
    }

}

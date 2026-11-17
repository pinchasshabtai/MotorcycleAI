using System.Text;
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

        var sql = new StringBuilder(
            """
            SELECT
                Id,
                Manufacturer,
                Model,
                [Year] AS [Year],
                EngineCC,
                Price,
                VehicleType,
                MotorcycleType,
                RiderExperience,
                [Usage] AS [Usage],
                Reliability,
                MaintenanceCost,
                Description,
                ImageUrl
            FROM dbo.Motorcycles
            WHERE 1 = 1
            """);
        var parameters = new DynamicParameters();

        AddRangeFilter(sql, parameters, "Price", ">=", "MinPrice", searchRequest.MinPrice);
        AddRangeFilter(sql, parameters, "Price", "<=", "MaxPrice", searchRequest.MaxPrice);
        AddRangeFilter(sql, parameters, "EngineCC", ">=", "MinEngineCC", searchRequest.MinEngineCC);
        AddRangeFilter(sql, parameters, "EngineCC", "<=", "MaxEngineCC", searchRequest.MaxEngineCC);
        AddRangeFilter(sql, parameters, "[Year]", ">=", "MinYear", searchRequest.MinYear);
        AddRangeFilter(sql, parameters, "[Year]", "<=", "MaxYear", searchRequest.MaxYear);

        var orderBy = new List<string>();
        AddPreferenceOrder(orderBy, "VehicleType", "@VehicleType", "VehicleType", searchRequest.VehicleType, parameters);
        AddPreferenceOrder(orderBy, "MotorcycleType", "@MotorcycleType", "MotorcycleType", searchRequest.MotorcycleType, parameters);
        AddPreferenceOrder(orderBy, "RiderExperience", "@RiderExperience", "RiderExperience", searchRequest.RiderExperience, parameters);
        AddPreferenceOrder(orderBy, "[Usage]", "@Usage", "Usage", searchRequest.Usage, parameters);
        AddPreferenceOrder(orderBy, "Reliability", "@Reliability", "Reliability", searchRequest.Reliability, parameters);
        AddPreferenceOrder(orderBy, "MaintenanceCost", "@MaintenanceCost", "MaintenanceCost", searchRequest.MaintenanceCost, parameters);

        if (searchRequest.PreferredBrands is { Count: > 0 })
        {
            orderBy.Add("CASE WHEN Manufacturer IN @PreferredBrands THEN 1 ELSE 0 END DESC");
            parameters.Add("PreferredBrands", searchRequest.PreferredBrands);
        }

        orderBy.Add("Price ASC");
        orderBy.Add("[Year] DESC");
        orderBy.Add("Manufacturer ASC");
        orderBy.Add("Model ASC");
        sql.AppendLine();
        sql.Append("ORDER BY ");
        sql.Append(string.Join(", ", orderBy));

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = new CommandDefinition(
            sql.ToString(),
            parameters,
            cancellationToken: cancellationToken);
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

    private static void AddRangeFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string columnName,
        string comparison,
        string parameterName,
        object? value)
    {
        if (value is null)
        {
            return;
        }

        sql.AppendLine()
            .Append("AND ")
            .Append(columnName)
            .Append(' ')
            .Append(comparison)
            .Append(" @")
            .Append(parameterName);
        parameters.Add(parameterName, value);
    }

    private static void AddPreferenceOrder(
        ICollection<string> orderBy,
        string columnName,
        string parameterName,
        string parameterKey,
        string? value,
        DynamicParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        orderBy.Add($"CASE WHEN {columnName} = {parameterName} THEN 1 ELSE 0 END DESC");
        parameters.Add(parameterKey, value);
    }
}

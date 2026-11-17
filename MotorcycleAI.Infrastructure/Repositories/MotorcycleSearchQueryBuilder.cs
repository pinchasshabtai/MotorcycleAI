using System.Text;
using Dapper;
using MotorcycleAI.Core.Models;

namespace MotorcycleAI.Infrastructure.Repositories;

internal static class MotorcycleSearchQueryBuilder
{
    internal static CommandDefinition Build(
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

        return new CommandDefinition(
            sql.ToString(),
            parameters,
            cancellationToken: cancellationToken);
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

using Dapper;
using MotorcycleAI.Core.Models;
using MotorcycleAI.Infrastructure.Repositories;

namespace MotorcycleAI.Tests.Unit;

public sealed class MotorcycleSearchQueryBuilderTests
{
    [Fact]
    public void Build_AddsParameterizedFiltersAndPreferences()
    {
        var request = new MotorcycleSearchRequest
        {
            MinPrice = 10_000,
            MaxPrice = 50_000,
            MinEngineCC = 125,
            MaxYear = 2025,
            MotorcycleType = "Naked",
            PreferredBrands = ["Honda"]
        };

        var command = MotorcycleSearchQueryBuilder.Build(request);
        var parameters = Assert.IsType<DynamicParameters>(command.Parameters);

        Assert.Contains("Price >= @MinPrice", command.CommandText);
        Assert.Contains("Price <= @MaxPrice", command.CommandText);
        Assert.Contains("EngineCC >= @MinEngineCC", command.CommandText);
        Assert.Contains("[Year] <= @MaxYear", command.CommandText);
        Assert.Contains("Manufacturer IN @PreferredBrands", command.CommandText);
        Assert.Equal(10_000m, parameters.Get<decimal>("MinPrice"));
        Assert.Equal(50_000m, parameters.Get<decimal>("MaxPrice"));
        Assert.Equal(125, parameters.Get<int>("MinEngineCC"));
        Assert.Equal(2025, parameters.Get<int>("MaxYear"));
        Assert.Equal("Naked", parameters.Get<string>("MotorcycleType"));
    }

    [Fact]
    public void Build_DoesNotInterpolateUserValuesIntoSql()
    {
        const string maliciousLookingValue = "'; DROP TABLE Motorcycles;--";
        var request = new MotorcycleSearchRequest
        {
            MotorcycleType = maliciousLookingValue
        };

        var command = MotorcycleSearchQueryBuilder.Build(request);
        var parameters = Assert.IsType<DynamicParameters>(command.Parameters);

        Assert.DoesNotContain(maliciousLookingValue, command.CommandText);
        Assert.Equal(maliciousLookingValue, parameters.Get<string>("MotorcycleType"));
    }

    [Fact]
    public void Build_OmitsFiltersForUnspecifiedValues()
    {
        var command = MotorcycleSearchQueryBuilder.Build(new MotorcycleSearchRequest());
        var parameters = Assert.IsType<DynamicParameters>(command.Parameters);

        Assert.DoesNotContain("AND Price", command.CommandText);
        Assert.DoesNotContain("AND EngineCC", command.CommandText);
        Assert.DoesNotContain("AND [Year]", command.CommandText);
        Assert.Empty(parameters.ParameterNames);
    }
}

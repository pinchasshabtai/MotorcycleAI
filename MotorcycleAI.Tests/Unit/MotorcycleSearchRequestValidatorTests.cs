using MotorcycleAI.Core.Models;
using MotorcycleAI.Core.Services;

namespace MotorcycleAI.Tests.Unit;

public sealed class MotorcycleSearchRequestValidatorTests
{
    [Fact]
    public void Validate_AllowsUnspecifiedFields()
    {
        var request = new MotorcycleSearchRequest();

        MotorcycleSearchRequestValidator.Validate(request);
    }

    [Fact]
    public void Validate_RejectsNegativePrice()
    {
        var request = new MotorcycleSearchRequest { MaxPrice = -1 };

        Assert.Throws<InvalidDataException>(
            () => MotorcycleSearchRequestValidator.Validate(request));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void Validate_RejectsUnreasonableEngineCapacity(int engineCC)
    {
        var request = new MotorcycleSearchRequest { MinEngineCC = engineCC };

        Assert.Throws<InvalidDataException>(
            () => MotorcycleSearchRequestValidator.Validate(request));
    }

    [Fact]
    public void Validate_AllowsZeroEngineCapacityForElectricVehicles()
    {
        var request = new MotorcycleSearchRequest
        {
            VehicleType = "Electric",
            MinEngineCC = 0,
            MaxEngineCC = 0
        };

        MotorcycleSearchRequestValidator.Validate(request);
    }

    [Fact]
    public void Validate_RejectsYearBeforeMotorcyclesExisted()
    {
        var request = new MotorcycleSearchRequest { MinYear = 1884 };

        Assert.Throws<InvalidDataException>(
            () => MotorcycleSearchRequestValidator.Validate(request));
    }

    [Fact]
    public void Validate_RejectsYearBeyondNextModelYear()
    {
        var request = new MotorcycleSearchRequest { MaxYear = DateTime.UtcNow.Year + 2 };

        Assert.Throws<InvalidDataException>(
            () => MotorcycleSearchRequestValidator.Validate(request));
    }

    [Fact]
    public void Validate_RejectsReversedRanges()
    {
        var request = new MotorcycleSearchRequest
        {
            MinPrice = 20_000,
            MaxPrice = 10_000
        };

        Assert.Throws<InvalidDataException>(
            () => MotorcycleSearchRequestValidator.Validate(request));
    }
}

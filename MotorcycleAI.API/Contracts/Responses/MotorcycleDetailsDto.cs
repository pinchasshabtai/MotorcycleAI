namespace MotorcycleAI.API.Contracts.Responses;

public sealed class MotorcycleDetailsDto
{
    public int Id { get; init; }

    public string Manufacturer { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public int Year { get; init; }

    public int EngineCC { get; init; }

    public decimal Price { get; init; }

    public bool PriceIsEstimate { get; init; }

    public bool AttributesAreEstimated { get; init; }

    public string? VehicleType { get; init; }

    public string MotorcycleType { get; init; } = string.Empty;

    public string? RiderExperience { get; init; }

    public string? Usage { get; init; }

    public string? Reliability { get; init; }

    public string? MaintenanceCost { get; init; }

    public string? Description { get; init; }

    public string? ImageUrl { get; init; }
}

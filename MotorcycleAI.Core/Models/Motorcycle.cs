namespace MotorcycleAI.Core.Models;

public sealed class Motorcycle
{
    public int Id { get; set; }

    public string Manufacturer { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int Year { get; set; }

    public int EngineCC { get; set; }

    public decimal Price { get; set; }

    public bool PriceIsEstimate { get; set; }

    public bool AttributesAreEstimated { get; set; }

    public string? VehicleType { get; set; }

    public string MotorcycleType { get; set; } = string.Empty;

    public string? RiderExperience { get; set; }

    public string? Usage { get; set; }

    public string? Reliability { get; set; }

    public string? MaintenanceCost { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }
}

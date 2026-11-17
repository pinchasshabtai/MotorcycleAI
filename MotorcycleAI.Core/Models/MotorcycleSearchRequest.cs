namespace MotorcycleAI.Core.Models;

public sealed class MotorcycleSearchRequest
{
    public string? VehicleType { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public int? MinEngineCC { get; set; }

    public int? MaxEngineCC { get; set; }

    public int? MinYear { get; set; }

    public int? MaxYear { get; set; }

    public string? RiderExperience { get; set; }

    public string? Usage { get; set; }

    public string? MotorcycleType { get; set; }

    public IReadOnlyList<string>? PreferredBrands { get; set; }

    public string? Reliability { get; set; }

    public string? MaintenanceCost { get; set; }

    public string? AdditionalPreferences { get; set; }
}

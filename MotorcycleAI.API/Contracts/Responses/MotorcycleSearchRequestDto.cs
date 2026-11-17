namespace MotorcycleAI.API.Contracts.Responses;

public sealed class MotorcycleSearchRequestDto
{
    public string? VehicleType { get; init; }

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public int? MinEngineCC { get; init; }

    public int? MaxEngineCC { get; init; }

    public int? MinYear { get; init; }

    public int? MaxYear { get; init; }

    public string? RiderExperience { get; init; }

    public string? Usage { get; init; }

    public string? MotorcycleType { get; init; }

    public IReadOnlyList<string>? PreferredBrands { get; init; }

    public string? Reliability { get; init; }

    public string? MaintenanceCost { get; init; }

    public string? AdditionalPreferences { get; init; }
}

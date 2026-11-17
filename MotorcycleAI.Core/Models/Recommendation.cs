namespace MotorcycleAI.Core.Models;

public sealed class Recommendation
{
    public int MotorcycleId { get; set; }

    public decimal MatchScore { get; set; }

    public string Reason { get; set; } = string.Empty;

    public IReadOnlyList<string> Advantages { get; set; } = [];

    public IReadOnlyList<string> Disadvantages { get; set; } = [];
}

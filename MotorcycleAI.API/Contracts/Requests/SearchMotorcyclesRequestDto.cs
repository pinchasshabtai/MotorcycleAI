using System.ComponentModel.DataAnnotations;

namespace MotorcycleAI.API.Contracts.Requests;

public sealed class SearchMotorcyclesRequestDto
{
    [Required]
    [StringLength(2000, MinimumLength = 3)]
    public string Query { get; init; } = string.Empty;
}

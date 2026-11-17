using Microsoft.AspNetCore.Mvc;
using MotorcycleAI.API.Contracts.Requests;
using MotorcycleAI.API.Contracts.Responses;
using MotorcycleAI.Core.Interfaces;
using MotorcycleAI.Core.Models;

namespace MotorcycleAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class MotorcyclesController : ControllerBase
{
    private readonly ISearchAnalyzer _searchAnalyzer;
    private readonly IMotorcycleRepository _motorcycleRepository;
    private readonly IRecommendationService _recommendationService;

    public MotorcyclesController(
        ISearchAnalyzer searchAnalyzer,
        IMotorcycleRepository motorcycleRepository,
        IRecommendationService recommendationService)
    {
        _searchAnalyzer = searchAnalyzer;
        _motorcycleRepository = motorcycleRepository;
        _recommendationService = recommendationService;
    }

    [HttpPost("search")]
    [ProducesResponseType<MotorcycleSearchResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<MotorcycleSearchResponseDto>> Search(
        [FromBody] SearchMotorcyclesRequestDto request,
        CancellationToken cancellationToken)
    {
        var searchRequest = await _searchAnalyzer.AnalyzeAsync(request.Query, cancellationToken);
        var motorcycles = await _motorcycleRepository.SearchAsync(searchRequest, cancellationToken);
        var recommendationResult = await _recommendationService.RecommendAsync(
            request.Query,
            searchRequest,
            motorcycles,
            cancellationToken);

        return Ok(new MotorcycleSearchResponseDto
        {
            SearchRequest = ToDto(searchRequest),
            Recommendations = recommendationResult.Recommendations
                .Select(ToDto)
                .ToArray(),
            Summary = recommendationResult.Summary
        });
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<MotorcycleDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MotorcycleDetailsDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid motorcycle ID",
                Detail = "The motorcycle ID must be a positive integer."
            });
        }

        var motorcycle = await _motorcycleRepository.GetByIdAsync(id, cancellationToken);
        return motorcycle is null ? NotFound() : Ok(ToDto(motorcycle));
    }

    private static MotorcycleSearchRequestDto ToDto(MotorcycleSearchRequest searchRequest)
    {
        return new MotorcycleSearchRequestDto
        {
            VehicleType = searchRequest.VehicleType,
            MinPrice = searchRequest.MinPrice,
            MaxPrice = searchRequest.MaxPrice,
            MinEngineCC = searchRequest.MinEngineCC,
            MaxEngineCC = searchRequest.MaxEngineCC,
            MinYear = searchRequest.MinYear,
            MaxYear = searchRequest.MaxYear,
            RiderExperience = searchRequest.RiderExperience,
            Usage = searchRequest.Usage,
            MotorcycleType = searchRequest.MotorcycleType,
            PreferredBrands = searchRequest.PreferredBrands,
            Reliability = searchRequest.Reliability,
            MaintenanceCost = searchRequest.MaintenanceCost,
            AdditionalPreferences = searchRequest.AdditionalPreferences
        };
    }

    private static MotorcycleRecommendationDto ToDto(Recommendation recommendation)
    {
        return new MotorcycleRecommendationDto
        {
            MotorcycleId = recommendation.MotorcycleId,
            MatchScore = recommendation.MatchScore,
            Reason = recommendation.Reason,
            Advantages = recommendation.Advantages,
            Disadvantages = recommendation.Disadvantages
        };
    }

    private static MotorcycleDetailsDto ToDto(Motorcycle motorcycle)
    {
        return new MotorcycleDetailsDto
        {
            Id = motorcycle.Id,
            Manufacturer = motorcycle.Manufacturer,
            Model = motorcycle.Model,
            Year = motorcycle.Year,
            EngineCC = motorcycle.EngineCC,
            Price = motorcycle.Price,
            PriceIsEstimate = motorcycle.PriceIsEstimate,
            AttributesAreEstimated = motorcycle.AttributesAreEstimated,
            VehicleType = motorcycle.VehicleType,
            MotorcycleType = motorcycle.MotorcycleType,
            RiderExperience = motorcycle.RiderExperience,
            Usage = motorcycle.Usage,
            Reliability = motorcycle.Reliability,
            MaintenanceCost = motorcycle.MaintenanceCost,
            Description = motorcycle.Description,
            ImageUrl = motorcycle.ImageUrl
        };
    }
}

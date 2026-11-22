using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using MotorcycleAI.API.Contracts.Responses;
using MotorcycleAI.Core.Interfaces;
using MotorcycleAI.Core.Models;

namespace MotorcycleAI.Tests.Integration;

public sealed class MotorcyclesApiTests
{
    [Fact]
    public async Task Search_ReturnsSearchRequestRecommendationsAndSummary()
    {
        var searchRequest = new MotorcycleSearchRequest { MaxPrice = 30_000 };
        var motorcycle = CreateMotorcycle(1);
        var analyzer = new Mock<ISearchAnalyzer>();
        analyzer
            .Setup(service => service.AnalyzeAsync("מחפש אופנוע עד 30 אלף", It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchRequest);
        var repository = new Mock<IMotorcycleRepository>();
        repository
            .Setup(service => service.SearchAsync(searchRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { motorcycle });
        var recommendationService = new Mock<IRecommendationService>();
        recommendationService
            .Setup(service => service.RecommendAsync(
                "מחפש אופנוע עד 30 אלף",
                searchRequest,
                It.Is<IReadOnlyList<Motorcycle>>(items => items.Count == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecommendationResult
            {
                Summary = "נמצאה התאמה.",
                Recommendations =
                [
                    new Recommendation
                    {
                        MotorcycleId = motorcycle.Id,
                        MatchScore = 100,
                        Reason = "עומד בתקציב.",
                        Advantages = ["מחיר בטווח."],
                        Disadvantages = []
                    }
                ]
            });

        using var factory = CreateFactory(analyzer, repository, recommendationService);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/motorcycles/search",
            new { query = "מחפש אופנוע עד 30 אלף" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MotorcycleSearchResponseDto>();
        Assert.NotNull(result);
        Assert.Equal(30_000, result.SearchRequest.MaxPrice);
        Assert.Equal("נמצאה התאמה.", result.Summary);
        var recommendation = Assert.Single(result.Recommendations);
        Assert.Equal(100, recommendation.MatchScore);
        Assert.Equal("ההתאמה הטובה ביותר", recommendation.RankLabel);
        Assert.Equal(motorcycle.Id, recommendation.Motorcycle.Id);
        Assert.Equal(motorcycle.EngineCC, recommendation.Motorcycle.EngineCC);
        analyzer.VerifyAll();
        repository.VerifyAll();
        recommendationService.VerifyAll();
    }

    [Fact]
    public async Task Search_ReturnsEmptyRecommendationsWhenNoMotorcyclesAreFound()
    {
        var searchRequest = new MotorcycleSearchRequest();
        var analyzer = new Mock<ISearchAnalyzer>();
        analyzer
            .Setup(service => service.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchRequest);
        var repository = new Mock<IMotorcycleRepository>();
        repository
            .Setup(service => service.SearchAsync(searchRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Motorcycle>());
        var recommendationService = new Mock<IRecommendationService>();
        recommendationService
            .Setup(service => service.RecommendAsync(
                It.IsAny<string>(),
                searchRequest,
                It.Is<IReadOnlyList<Motorcycle>>(items => items.Count == 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecommendationResult
            {
                Summary = "לא נמצאו אופנועים מתאימים.",
                Recommendations = []
            });

        using var factory = CreateFactory(analyzer, repository, recommendationService);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/motorcycles/search",
            new { query = "מחפש אופנוע" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MotorcycleSearchResponseDto>();
        Assert.NotNull(result);
        Assert.Empty(result.Recommendations);
        Assert.Equal("לא נמצאו אופנועים מתאימים.", result.Summary);
    }

    [Fact]
    public async Task Search_RejectsInvalidRequestBody()
    {
        var analyzer = new Mock<ISearchAnalyzer>();
        var repository = new Mock<IMotorcycleRepository>();
        var recommendationService = new Mock<IRecommendationService>();
        using var factory = CreateFactory(analyzer, repository, recommendationService);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/motorcycles/search",
            new { query = "hi" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        analyzer.VerifyNoOtherCalls();
        repository.VerifyNoOtherCalls();
        recommendationService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Search_MapsUpstreamFailureToServiceUnavailableWithoutStackTrace()
    {
        var analyzer = new Mock<ISearchAnalyzer>();
        analyzer
            .Setup(service => service.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("private upstream detail"));
        var repository = new Mock<IMotorcycleRepository>();
        var recommendationService = new Mock<IRecommendationService>();
        using var factory = CreateFactory(analyzer, repository, recommendationService);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/motorcycles/search",
            new { query = "מחפש אופנוע" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("private upstream detail", body);
        Assert.DoesNotContain(" at ", body);
        Assert.Contains("traceId", body);
    }

    [Fact]
    public async Task GetById_ReturnsMotorcycleDto()
    {
        var motorcycle = CreateMotorcycle(7);
        motorcycle.PriceIsEstimate = true;
        motorcycle.AttributesAreEstimated = true;
        var analyzer = new Mock<ISearchAnalyzer>();
        var repository = new Mock<IMotorcycleRepository>();
        repository
            .Setup(service => service.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(motorcycle);
        var recommendationService = new Mock<IRecommendationService>();
        using var factory = CreateFactory(analyzer, repository, recommendationService);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/motorcycles/7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MotorcycleDetailsDto>();
        Assert.NotNull(result);
        Assert.Equal(7, result.Id);
        Assert.Equal("Honda", result.Manufacturer);
        Assert.True(result.PriceIsEstimate);
        Assert.True(result.AttributesAreEstimated);
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundWhenMotorcycleDoesNotExist()
    {
        var analyzer = new Mock<ISearchAnalyzer>();
        var repository = new Mock<IMotorcycleRepository>();
        repository
            .Setup(service => service.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Motorcycle?)null);
        var recommendationService = new Mock<IRecommendationService>();
        using var factory = CreateFactory(analyzer, repository, recommendationService);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/motorcycles/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        Mock<ISearchAnalyzer> analyzer,
        Mock<IMotorcycleRepository> repository,
        Mock<IRecommendationService> recommendationService)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISearchAnalyzer>();
                services.RemoveAll<IMotorcycleRepository>();
                services.RemoveAll<IRecommendationService>();
                services.AddSingleton(analyzer.Object);
                services.AddSingleton(repository.Object);
                services.AddSingleton(recommendationService.Object);
            });
        });
    }

    private static Motorcycle CreateMotorcycle(int id)
    {
        return new Motorcycle
        {
            Id = id,
            Manufacturer = "Honda",
            Model = "Example",
            Year = 2024,
            EngineCC = 350,
            Price = 25_000,
            PriceIsEstimate = false,
            MotorcycleType = "Naked"
        };
    }
}

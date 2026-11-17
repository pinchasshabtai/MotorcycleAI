using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MotorcycleAI.Core.Interfaces;
using MotorcycleAI.Core.Models;
using MotorcycleAI.Core.Services;

namespace MotorcycleAI.Tests.Unit;

public sealed class RecommendationServiceTests
{
    [Fact]
    public async Task RecommendAsync_ScoresAndOrdersMotorcyclesUsingCSharpCriteria()
    {
        const string responseJson = """
            {
              "summary": "האופנוע הראשון תואם לדרישות יותר מהשני.",
              "recommendations": [
                { "motorcycleId": 2, "reason": "יש פערים מול הדרישות." },
                { "motorcycleId": 1, "reason": "הוא תואם לכל הדרישות שסופקו." }
              ]
            }
            """;
        var capturedSchema = default(JsonElement);
        var generator = CreateGenerator(responseJson, schema => capturedSchema = schema);
        var service = new RecommendationService(generator.Object, NullLogger<RecommendationService>.Instance);
        var request = new MotorcycleSearchRequest
        {
            MinPrice = 10_000,
            MaxPrice = 50_000,
            MotorcycleType = "Naked",
            PreferredBrands = ["Honda"]
        };
        var motorcycles = new[]
        {
            new Motorcycle
            {
                Id = 1,
                Manufacturer = "Honda",
                Model = "Example A",
                Year = 2023,
                EngineCC = 400,
                Price = 30_000,
                MotorcycleType = "Naked"
            },
            new Motorcycle
            {
                Id = 2,
                Manufacturer = "Yamaha",
                Model = "Example B",
                Year = 2021,
                EngineCC = 650,
                Price = 60_000,
                MotorcycleType = "Adventure"
            }
        };

        var result = await service.RecommendAsync("אופנוע עד 50 אלף", request, motorcycles);

        Assert.Equal(new[] { 1, 2 }, result.Recommendations.Select(item => item.MotorcycleId));
        Assert.Equal(100m, result.Recommendations[0].MatchScore);
        Assert.Equal(0m, result.Recommendations[1].MatchScore);
        Assert.NotEmpty(result.Recommendations[0].Advantages);
        Assert.Empty(result.Recommendations[0].Disadvantages);
        Assert.NotEmpty(result.Recommendations[1].Disadvantages);
        Assert.False(capturedSchema.GetProperty("properties").TryGetProperty("matchScore", out _));
    }

    [Fact]
    public async Task RecommendAsync_ReturnsNeutralScoreWhenNoCriteriaWereProvided()
    {
        var generator = CreateGenerator(
            """{"summary":"סיכום","recommendations":[{"motorcycleId":1,"reason":"הצעה כללית."}]}""");
        var service = new RecommendationService(generator.Object, NullLogger<RecommendationService>.Instance);

        var result = await service.RecommendAsync(
            "מחפש אופנוע",
            new MotorcycleSearchRequest(),
            [CreateMotorcycle(1)]);

        Assert.Equal(50m, Assert.Single(result.Recommendations).MatchScore);
    }

    [Fact]
    public async Task RecommendAsync_ReturnsSummaryWithoutCallingAiWhenNoMotorcyclesWereFound()
    {
        var generator = new Mock<IStructuredResponseGenerator>();
        var service = new RecommendationService(generator.Object, NullLogger<RecommendationService>.Instance);

        var result = await service.RecommendAsync(
            "מחפש אופנוע",
            new MotorcycleSearchRequest(),
            []);

        Assert.Empty(result.Recommendations);
        Assert.False(string.IsNullOrWhiteSpace(result.Summary));
        generator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RecommendAsync_RejectsUnknownMotorcycleIdsFromAi()
    {
        var generator = CreateGenerator(
            """{"summary":"סיכום","recommendations":[{"motorcycleId":99,"reason":"לא מוכר."}]}""");
        var service = new RecommendationService(generator.Object, NullLogger<RecommendationService>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.RecommendAsync(
                "מחפש אופנוע",
                new MotorcycleSearchRequest(),
                [CreateMotorcycle(1)]));
    }

    private static Mock<IStructuredResponseGenerator> CreateGenerator(
        string response,
        Action<JsonElement>? onSchema = null)
    {
        var generator = new Mock<IStructuredResponseGenerator>();
        generator
            .Setup(service => service.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<JsonElement>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, JsonElement, CancellationToken>(
                (_, _, schema, _) => onSchema?.Invoke(schema.Clone()))
            .ReturnsAsync(response);

        return generator;
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
            MotorcycleType = "Naked"
        };
    }
}

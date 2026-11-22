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
              "recommendations": {
                "2": { "reason": "יש פערים מול הדרישות." },
                "1": { "reason": "הוא תואם לכל הדרישות שסופקו." }
              }
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
        var recommendationsSchema = capturedSchema
            .GetProperty("properties")
            .GetProperty("recommendations");
        Assert.Equal(
            new[] { "1", "2" },
            recommendationsSchema.GetProperty("required")
                .EnumerateArray()
                .Select(id => id.GetString())
                .ToArray());
        Assert.False(recommendationsSchema.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public async Task RecommendAsync_ReturnsNeutralScoreWhenNoCriteriaWereProvided()
    {
        var generator = CreateGenerator(
            """{"summary":"סיכום","recommendations":{"1":{"reason":"הצעה כללית."}}}""");
        var service = new RecommendationService(generator.Object, NullLogger<RecommendationService>.Instance);

        var result = await service.RecommendAsync(
            "מחפש אופנוע",
            new MotorcycleSearchRequest(),
            [CreateMotorcycle(1)]);

        Assert.Equal(50m, Assert.Single(result.Recommendations).MatchScore);
    }

    [Fact]
    public async Task RecommendAsync_ReturnsOnlyThreeHighestScoringMotorcycles()
    {
        var generator = CreateGenerator(
            """
            {
              "summary": "שלוש ההמלצות המובילות.",
              "recommendations": {
                "2": { "reason": "הכי מתאימה." },
                "4": { "reason": "מתאימה באמצע." },
                "3": { "reason": "הכי פחות מתאימה מבין השלוש." }
              }
            }
            """);
        var service = new RecommendationService(generator.Object, NullLogger<RecommendationService>.Instance);
        var motorcycles = new[]
        {
            CreateMotorcycle(1),
            CreateMotorcycle(2),
            CreateMotorcycle(3),
            CreateMotorcycle(4),
            CreateMotorcycle(5)
        };
        motorcycles[0].Price = 50_000;
        motorcycles[1].Price = 10_000;
        motorcycles[2].Price = 30_000;
        motorcycles[3].Price = 20_000;
        motorcycles[4].Price = 40_000;

        var result = await service.RecommendAsync(
            "מחפש אופנוע",
            new MotorcycleSearchRequest(),
            motorcycles);

        Assert.Equal(new[] { 2, 4, 3 }, result.Recommendations.Select(item => item.MotorcycleId));
        Assert.Equal(3, result.Recommendations.Count);
    }

    [Fact]
    public async Task RecommendAsync_DoesNotPenalizeUnratedReliability()
    {
        var generator = CreateGenerator(
            """{"summary":"סיכום","recommendations":{"1":{"reason":"האמינות אינה מדורגת במאגר."}}}""");
        var service = new RecommendationService(generator.Object, NullLogger<RecommendationService>.Instance);
        var motorcycle = CreateMotorcycle(1);
        motorcycle.Reliability = "Not rated";

        var result = await service.RecommendAsync(
            "מחפש אופנוע אמין",
            new MotorcycleSearchRequest { Reliability = "High" },
            [motorcycle]);

        var recommendation = Assert.Single(result.Recommendations);
        Assert.Equal(50m, recommendation.MatchScore);
        Assert.Contains(recommendation.Disadvantages, item => item.Contains("אין נתון מאומת"));
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
            """{"summary":"סיכום","recommendations":{"99":{"reason":"לא מוכר."}}}""");
        var service = new RecommendationService(generator.Object, NullLogger<RecommendationService>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.RecommendAsync(
                "מחפש אופנוע",
                new MotorcycleSearchRequest(),
                [CreateMotorcycle(1)]));
    }

    [Fact]
    public async Task RecommendAsync_RejectsDuplicateMotorcycleIdsFromAi()
    {
        var generator = CreateGenerator(
            """{"summary":"סיכום","recommendations":{"1":{"reason":"ראשון."},"1":{"reason":"כפול."}}}""");
        var service = new RecommendationService(generator.Object, NullLogger<RecommendationService>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.RecommendAsync(
                "מחפש אופנוע",
                new MotorcycleSearchRequest(),
                [CreateMotorcycle(1)]));
    }

    [Fact]
    public async Task RecommendAsync_RejectsMissingMotorcycleIdsFromAi()
    {
        var generator = CreateGenerator(
            """{"summary":"סיכום","recommendations":{"1":{"reason":"קיים."}}}""");
        var service = new RecommendationService(generator.Object, NullLogger<RecommendationService>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.RecommendAsync(
                "מחפש אופנוע",
                new MotorcycleSearchRequest(),
                [CreateMotorcycle(1), CreateMotorcycle(2)]));
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

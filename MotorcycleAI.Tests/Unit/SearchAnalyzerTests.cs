using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MotorcycleAI.Core.Interfaces;
using MotorcycleAI.Core.Models;
using MotorcycleAI.Core.Services;

namespace MotorcycleAI.Tests.Unit;

public sealed class SearchAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_DeserializesStructuredResponseAndPreservesNulls()
    {
        var response = JsonSerializer.Serialize(new MotorcycleSearchRequest
        {
            MaxPrice = 30_000,
            RiderExperience = "מתחיל",
            PreferredBrands = ["Honda"]
        });
        var capturedQuery = string.Empty;
        var capturedSchema = default(JsonElement);
        var generator = CreateGenerator(
            response,
            (query, schema) =>
            {
                capturedQuery = query;
                capturedSchema = schema;
            });
        var analyzer = new SearchAnalyzer(generator.Object, NullLogger<SearchAnalyzer>.Instance);

        var result = await analyzer.AnalyzeAsync("אני מחפש אופנוע עד 30 אלף שקל שמתאים למתחילים");

        Assert.Equal("אני מחפש אופנוע עד 30 אלף שקל שמתאים למתחילים", capturedQuery);
        Assert.Equal(30_000, result.MaxPrice);
        Assert.Equal("מתחיל", result.RiderExperience);
        Assert.Equal("Honda", Assert.Single(result.PreferredBrands!));
        Assert.Null(result.MinPrice);
        Assert.Null(result.MinEngineCC);
        Assert.Equal(14, capturedSchema.GetProperty("required").GetArrayLength());

        var nullablePriceTypes = capturedSchema
            .GetProperty("properties")
            .GetProperty("MinPrice")
            .GetProperty("type")
            .EnumerateArray()
            .Select(type => type.GetString())
            .ToArray();
        Assert.Contains("number", nullablePriceTypes);
        Assert.Contains("null", nullablePriceTypes);
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsInvalidJson()
    {
        var generator = CreateGenerator("{ invalid json");
        var analyzer = new SearchAnalyzer(generator.Object, NullLogger<SearchAnalyzer>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => analyzer.AnalyzeAsync("מחפש אופנוע"));
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsMissingFields()
    {
        var generator = CreateGenerator("""{"VehicleType":null}""");
        var analyzer = new SearchAnalyzer(generator.Object, NullLogger<SearchAnalyzer>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => analyzer.AnalyzeAsync("מחפש אופנוע"));

        Assert.Contains("MinPrice", exception.Message);
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsNegativePrice()
    {
        var response = JsonSerializer.Serialize(new MotorcycleSearchRequest { MinPrice = -1 });
        var generator = CreateGenerator(response);
        var analyzer = new SearchAnalyzer(generator.Object, NullLogger<SearchAnalyzer>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => analyzer.AnalyzeAsync("מחפש אופנוע"));
    }

    [Fact]
    public async Task AnalyzeAsync_PropagatesCommunicationFailures()
    {
        var generator = new Mock<IStructuredResponseGenerator>();
        generator
            .Setup(service => service.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<JsonElement>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Ollama unavailable"));
        var analyzer = new SearchAnalyzer(generator.Object, NullLogger<SearchAnalyzer>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => analyzer.AnalyzeAsync("מחפש אופנוע"));
    }

    private static Mock<IStructuredResponseGenerator> CreateGenerator(
        string response,
        Action<string, JsonElement>? onRequest = null)
    {
        var generator = new Mock<IStructuredResponseGenerator>();
        generator
            .Setup(service => service.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<JsonElement>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, JsonElement, CancellationToken>(
                (_, query, schema, _) =>
                {
                    onRequest?.Invoke(query, schema.Clone());
                })
            .ReturnsAsync(response);

        return generator;
    }
}

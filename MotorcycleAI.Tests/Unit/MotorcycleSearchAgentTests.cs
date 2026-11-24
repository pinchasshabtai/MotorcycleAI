using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MotorcycleAI.Core.Interfaces;
using MotorcycleAI.Core.Models;
using MotorcycleAI.Core.Services;

namespace MotorcycleAI.Tests.Unit;

public sealed class MotorcycleSearchAgentTests
{
    [Fact]
    public async Task SearchAsync_InvokesSearchToolAndExecutesItAgainstRepository()
    {
        var searchRequest = new MotorcycleSearchRequest
        {
            MaxPrice = 30_000,
            RiderExperience = "מתחיל",
            PreferredBrands = ["Honda"]
        };
        var repositoryResult = new[] { new Motorcycle { Id = 1, Manufacturer = "Honda", Model = "CB" } };
        var generator = new Mock<IStructuredResponseGenerator>();
        JsonElement capturedTools = default;
        generator
            .Setup(service => service.GenerateToolCallsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<JsonElement>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, JsonElement, CancellationToken>(
                (_, query, tools, _) =>
                {
                    Assert.Equal("מחפש אופנוע עד 30 אלף שמתאים למתחילים", query);
                    capturedTools = tools.Clone();
                })
            .ReturnsAsync(
            [
                new ModelToolCall(
                    "SearchMotorcycles",
                    JsonSerializer.SerializeToElement(searchRequest))
            ]);
        var repository = new Mock<IMotorcycleRepository>();
        repository
            .Setup(service => service.SearchAsync(
                It.Is<MotorcycleSearchRequest>(request =>
                    request.MaxPrice == 30_000
                    && request.RiderExperience == "מתחיל"
                    && request.PreferredBrands!.SequenceEqual(new[] { "Honda" })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(repositoryResult);
        var agent = new MotorcycleSearchAgent(
            generator.Object,
            repository.Object,
            NullLogger<MotorcycleSearchAgent>.Instance);

        var result = await agent.SearchAsync("מחפש אופנוע עד 30 אלף שמתאים למתחילים");

        Assert.Equal(30_000, result.SearchRequest.MaxPrice);
        Assert.Equal("מתחיל", result.SearchRequest.RiderExperience);
        Assert.Same(repositoryResult[0], Assert.Single(result.Motorcycles));
        var tool = Assert.Single(capturedTools.EnumerateArray());
        Assert.Equal("function", tool.GetProperty("type").GetString());
        Assert.Equal("SearchMotorcycles", tool.GetProperty("function").GetProperty("name").GetString());
        Assert.Equal(14, tool.GetProperty("function").GetProperty("parameters").GetProperty("required").GetArrayLength());
        generator.VerifyAll();
        repository.VerifyAll();
    }

    [Fact]
    public async Task SearchAsync_RejectsUnsupportedToolCalls()
    {
        var generator = new Mock<IStructuredResponseGenerator>();
        generator
            .Setup(service => service.GenerateToolCallsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<JsonElement>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ModelToolCall("DeleteMotorcycles", JsonSerializer.SerializeToElement(new { }))]);
        var repository = new Mock<IMotorcycleRepository>();
        var agent = new MotorcycleSearchAgent(
            generator.Object,
            repository.Object,
            NullLogger<MotorcycleSearchAgent>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(() => agent.SearchAsync("מחפש אופנוע"));

        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SearchAsync_RejectsInvalidToolArgumentsBeforeDatabaseAccess()
    {
        var invalidArguments = JsonSerializer.SerializeToElement(
            new MotorcycleSearchRequest { MinPrice = -1 });
        var generator = new Mock<IStructuredResponseGenerator>();
        generator
            .Setup(service => service.GenerateToolCallsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<JsonElement>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ModelToolCall("SearchMotorcycles", invalidArguments)]);
        var repository = new Mock<IMotorcycleRepository>();
        var agent = new MotorcycleSearchAgent(
            generator.Object,
            repository.Object,
            NullLogger<MotorcycleSearchAgent>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(() => agent.SearchAsync("מחפש אופנוע"));

        repository.VerifyNoOtherCalls();
    }
}

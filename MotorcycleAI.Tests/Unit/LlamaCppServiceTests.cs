using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MotorcycleAI.Infrastructure.LlamaCpp;

namespace MotorcycleAI.Tests.Unit;

public sealed class LlamaCppServiceTests
{
    [Fact]
    public async Task GenerateAsync_SendsOpenAiCompatibleStructuredRequest()
    {
        var handler = new CallbackHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri("http://localhost:8080/v1/chat/completions"), request.RequestUri);

            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(body);
            Assert.Equal("local-model", json.RootElement.GetProperty("model").GetString());
            Assert.False(json.RootElement.GetProperty("stream").GetBoolean());
            Assert.Equal("json_schema", json.RootElement.GetProperty("response_format").GetProperty("type").GetString());
            Assert.True(json.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("strict").GetBoolean());
            Assert.Equal(2, json.RootElement.GetProperty("messages").GetArrayLength());

            return JsonResponse(HttpStatusCode.OK, """{"choices":[{"message":{"content":"{\"ok\":true}"}}]}""");
        });
        using var client = CreateHttpClient(handler);
        using var schemaDocument = JsonDocument.Parse("""{"type":"object","properties":{"ok":{"type":"boolean"}}}""");
        var service = CreateService(client);

        var response = await service.GenerateAsync(
            "system prompt",
            "בקשה בעברית",
            schemaDocument.RootElement);

        Assert.Equal("""{"ok":true}""", response);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsOnHttpError()
    {
        var handler = new CallbackHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.ServiceUnavailable, "{}")));
        using var client = CreateHttpClient(handler);
        using var schemaDocument = JsonDocument.Parse("""{"type":"object"}""");
        var service = CreateService(client);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.GenerateAsync("system", "user", schemaDocument.RootElement));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    [Fact]
    public async Task GenerateToolCallsAsync_SendsToolsAndParsesStringArguments()
    {
        var handler = new CallbackHandler(async (request, cancellationToken) =>
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(body);
            Assert.Equal(1, json.RootElement.GetProperty("tools").GetArrayLength());
            Assert.False(json.RootElement.TryGetProperty("response_format", out _));
            return JsonResponse(
                HttpStatusCode.OK,
                """{"choices":[{"message":{"tool_calls":[{"function":{"name":"SearchMotorcycles","arguments":"{\"MaxPrice\":30000}"}}]}}]}""");
        });
        using var client = CreateHttpClient(handler);
        using var toolsDocument = JsonDocument.Parse(
            """[{"type":"function","function":{"name":"SearchMotorcycles","parameters":{"type":"object"}}}]""");
        var service = CreateService(client);

        var toolCall = Assert.Single(await service.GenerateToolCallsAsync(
            "system",
            "מחפש עד 30000",
            toolsDocument.RootElement));

        Assert.Equal("SearchMotorcycles", toolCall.Name);
        Assert.Equal(30_000, toolCall.Arguments.GetProperty("MaxPrice").GetInt32());
    }

    [Fact]
    public async Task GenerateAsync_RejectsResponseWithoutChoices()
    {
        var handler = new CallbackHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"choices":[]}""")));
        using var client = CreateHttpClient(handler);
        using var schemaDocument = JsonDocument.Parse("""{"type":"object"}""");
        var service = CreateService(client);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.GenerateAsync("system", "user", schemaDocument.RootElement));
    }

    private static LlamaCppService CreateService(HttpClient client)
    {
        return new LlamaCppService(
            client,
            Options.Create(new LlamaCppOptions
            {
                BaseUrl = "http://localhost:8080",
                Model = "local-model",
                TimeoutSeconds = 10
            }),
            NullLogger<LlamaCppService>.Instance);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string content)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }

    private sealed class CallbackHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return callback(request, cancellationToken);
        }
    }
}

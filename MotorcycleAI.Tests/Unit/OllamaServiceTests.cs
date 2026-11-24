using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MotorcycleAI.Infrastructure.Ollama;

namespace MotorcycleAI.Tests.Unit;

public sealed class OllamaServiceTests
{
    [Fact]
    public async Task GenerateAsync_SendsStructuredChatRequestWithoutStreaming()
    {
        var handler = new CallbackHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri("http://localhost:11434/api/chat"), request.RequestUri);

            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(body);
            Assert.Equal("qwen3:8b", json.RootElement.GetProperty("model").GetString());
            Assert.False(json.RootElement.GetProperty("stream").GetBoolean());
            Assert.Equal(JsonValueKind.Object, json.RootElement.GetProperty("format").ValueKind);
            Assert.Equal(2, json.RootElement.GetProperty("messages").GetArrayLength());

            return JsonResponse(HttpStatusCode.OK, """{"message":{"content":"{\"ok\":true}"}}""");
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
    public async Task GenerateAsync_RejectsInvalidResponseJson()
    {
        var handler = new CallbackHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "not json")));
        using var client = CreateHttpClient(handler);
        using var schemaDocument = JsonDocument.Parse("""{"type":"object"}""");
        var service = CreateService(client);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.GenerateAsync("system", "user", schemaDocument.RootElement));
    }

    [Fact]
    public async Task GenerateAsync_RejectsResponseWithoutMessageContent()
    {
        var handler = new CallbackHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"message":{}}""")));
        using var client = CreateHttpClient(handler);
        using var schemaDocument = JsonDocument.Parse("""{"type":"object"}""");
        var service = CreateService(client);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.GenerateAsync("system", "user", schemaDocument.RootElement));
    }

    private static OllamaService CreateService(HttpClient client)
    {
        return new OllamaService(
            client,
            Options.Create(new OllamaOptions
            {
                BaseUrl = "http://localhost:11434",
                Model = "qwen3:8b",
                TimeoutSeconds = 10
            }),
            NullLogger<OllamaService>.Instance);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434")
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

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MotorcycleAI.Core.Interfaces;
using MotorcycleAI.Core.Models;

namespace MotorcycleAI.Infrastructure.Ollama;

public sealed class OllamaService : IStructuredResponseGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaService> _logger;

    public OllamaService(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        JsonElement jsonSchema,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(userPrompt);
        if (jsonSchema.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The JSON Schema must be a JSON object.", nameof(jsonSchema));
        }

        var request = new OllamaChatRequest
        {
            Model = _options.Model,
            Messages =
            [
                new OllamaChatMessage { Role = "system", Content = systemPrompt },
                new OllamaChatMessage { Role = "user", Content = userPrompt }
            ],
            Format = jsonSchema,
            Stream = false
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                "api/chat",
                request,
                JsonOptions,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "The request to Ollama failed.");
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "The request to Ollama timed out.");
            throw new TimeoutException("The request to Ollama timed out.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Ollama returned HTTP status code {StatusCode}.",
                    (int)response.StatusCode);
                throw new HttpRequestException(
                    $"Ollama returned HTTP status code {(int)response.StatusCode}.",
                    null,
                    response.StatusCode);
            }

            OllamaChatResponse? chatResponse;
            try
            {
                chatResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                    JsonOptions,
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                _logger.LogError(exception, "Ollama returned an invalid response payload.");
                throw new InvalidDataException("Ollama returned an invalid response payload.", exception);
            }

            var content = chatResponse?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogError("Ollama returned a response without message content.");
                throw new InvalidDataException("Ollama returned a response without message content.");
            }

            return content;
        }
    }

    public async Task<IReadOnlyList<ModelToolCall>> GenerateToolCallsAsync(
        string systemPrompt,
        string userPrompt,
        JsonElement tools,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(userPrompt);
        if (tools.ValueKind != JsonValueKind.Array || tools.GetArrayLength() == 0)
        {
            throw new ArgumentException("At least one tool must be provided.", nameof(tools));
        }

        var request = new OllamaChatRequest
        {
            Model = _options.Model,
            Messages =
            [
                new OllamaChatMessage { Role = "system", Content = systemPrompt },
                new OllamaChatMessage { Role = "user", Content = userPrompt }
            ],
            Tools = tools,
            Stream = false
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                "api/chat",
                request,
                JsonOptions,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "The request to Ollama failed.");
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "The request to Ollama timed out.");
            throw new TimeoutException("The request to Ollama timed out.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Ollama returned HTTP status code {StatusCode}.",
                    (int)response.StatusCode);
                throw new HttpRequestException(
                    $"Ollama returned HTTP status code {(int)response.StatusCode}.",
                    null,
                    response.StatusCode);
            }

            OllamaChatResponse? chatResponse;
            try
            {
                chatResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                    JsonOptions,
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                _logger.LogError(exception, "Ollama returned an invalid response payload.");
                throw new InvalidDataException("Ollama returned an invalid response payload.", exception);
            }

            var toolCalls = chatResponse?.Message?.ToolCalls;
            if (toolCalls is not { Count: > 0 })
            {
                _logger.LogError("Ollama returned a response without tool calls.");
                throw new InvalidDataException("Ollama did not call a tool.");
            }

            if (toolCalls.Any(toolCall =>
                toolCall?.Function is null
                || string.IsNullOrWhiteSpace(toolCall.Function.Name)))
            {
                _logger.LogError("Ollama returned a malformed tool call.");
                throw new InvalidDataException("Ollama returned a malformed tool call.");
            }

            return toolCalls
                .Select(toolCall => new ModelToolCall(
                    toolCall.Function!.Name,
                    NormalizeArguments(toolCall.Function.Arguments)))
                .ToArray();
        }
    }

    private static JsonElement NormalizeArguments(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.String)
        {
            return arguments.Clone();
        }

        try
        {
            using var document = JsonDocument.Parse(arguments.GetString()!);
            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Ollama returned invalid tool call arguments.", exception);
        }
    }

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("messages")]
        public IReadOnlyList<OllamaChatMessage> Messages { get; init; } = [];

        [JsonPropertyName("format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? Format { get; init; }

        [JsonPropertyName("tools")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? Tools { get; init; }

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }
    }

    private sealed class OllamaChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; init; } = string.Empty;
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaResponseMessage? Message { get; init; }
    }

    private sealed class OllamaResponseMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }

        [JsonPropertyName("tool_calls")]
        public IReadOnlyList<OllamaToolCall>? ToolCalls { get; init; }
    }

    private sealed class OllamaToolCall
    {
        [JsonPropertyName("function")]
        public OllamaToolFunction? Function { get; init; }
    }

    private sealed class OllamaToolFunction
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("arguments")]
        public JsonElement Arguments { get; init; }
    }
}

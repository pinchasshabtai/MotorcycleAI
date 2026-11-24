using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MotorcycleAI.Core.Interfaces;
using MotorcycleAI.Core.Models;

namespace MotorcycleAI.Infrastructure.LlamaCpp;

public sealed class LlamaCppService : IStructuredResponseGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly LlamaCppOptions _options;
    private readonly ILogger<LlamaCppService> _logger;

    public LlamaCppService(
        HttpClient httpClient,
        IOptions<LlamaCppOptions> options,
        ILogger<LlamaCppService> logger)
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

        var request = new LlamaCppChatRequest
        {
            Model = _options.Model,
            Messages =
            [
                new LlamaCppChatMessage { Role = "system", Content = systemPrompt },
                new LlamaCppChatMessage { Role = "user", Content = userPrompt }
            ],
            ResponseFormat = new LlamaCppResponseFormat
            {
                Type = "json_schema",
                JsonSchema = new LlamaCppJsonSchema
                {
                    Name = "structured_response",
                    Strict = true,
                    Schema = jsonSchema
                }
            },
            Stream = false
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                "v1/chat/completions",
                request,
                JsonOptions,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "The request to llama.cpp failed.");
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "The request to llama.cpp timed out.");
            throw new TimeoutException("The request to llama.cpp timed out.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "llama.cpp returned HTTP status code {StatusCode}.",
                    (int)response.StatusCode);
                throw new HttpRequestException(
                    $"llama.cpp returned HTTP status code {(int)response.StatusCode}.",
                    null,
                    response.StatusCode);
            }

            LlamaCppChatResponse? chatResponse;
            try
            {
                chatResponse = await response.Content.ReadFromJsonAsync<LlamaCppChatResponse>(
                    JsonOptions,
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                _logger.LogError(exception, "llama.cpp returned an invalid response payload.");
                throw new InvalidDataException("llama.cpp returned an invalid response payload.", exception);
            }

            var content = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogError("llama.cpp returned a response without message content.");
                throw new InvalidDataException("llama.cpp returned a response without message content.");
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

        var request = new LlamaCppChatRequest
        {
            Model = _options.Model,
            Messages =
            [
                new LlamaCppChatMessage { Role = "system", Content = systemPrompt },
                new LlamaCppChatMessage { Role = "user", Content = userPrompt }
            ],
            Tools = tools,
            Stream = false
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                "v1/chat/completions",
                request,
                JsonOptions,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "The request to llama.cpp failed.");
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "The request to llama.cpp timed out.");
            throw new TimeoutException("The request to llama.cpp timed out.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "llama.cpp returned HTTP status code {StatusCode}.",
                    (int)response.StatusCode);
                throw new HttpRequestException(
                    $"llama.cpp returned HTTP status code {(int)response.StatusCode}.",
                    null,
                    response.StatusCode);
            }

            LlamaCppChatResponse? chatResponse;
            try
            {
                chatResponse = await response.Content.ReadFromJsonAsync<LlamaCppChatResponse>(
                    JsonOptions,
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                _logger.LogError(exception, "llama.cpp returned an invalid response payload.");
                throw new InvalidDataException("llama.cpp returned an invalid response payload.", exception);
            }

            var toolCalls = chatResponse?.Choices?.FirstOrDefault()?.Message?.ToolCalls;
            if (toolCalls is not { Count: > 0 })
            {
                _logger.LogError("llama.cpp returned a response without tool calls.");
                throw new InvalidDataException("llama.cpp did not call a tool.");
            }

            if (toolCalls.Any(toolCall =>
                toolCall?.Function is null
                || string.IsNullOrWhiteSpace(toolCall.Function.Name)))
            {
                _logger.LogError("llama.cpp returned a malformed tool call.");
                throw new InvalidDataException("llama.cpp returned a malformed tool call.");
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
            throw new InvalidDataException("llama.cpp returned invalid tool call arguments.", exception);
        }
    }

    private sealed class LlamaCppChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("messages")]
        public IReadOnlyList<LlamaCppChatMessage> Messages { get; init; } = [];

        [JsonPropertyName("tools")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? Tools { get; init; }

        [JsonPropertyName("response_format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LlamaCppResponseFormat? ResponseFormat { get; init; }

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }
    }

    private sealed class LlamaCppChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; init; } = string.Empty;
    }

    private sealed class LlamaCppResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("json_schema")]
        public LlamaCppJsonSchema JsonSchema { get; init; } = new();
    }

    private sealed class LlamaCppJsonSchema
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("strict")]
        public bool Strict { get; init; }

        [JsonPropertyName("schema")]
        public JsonElement Schema { get; init; }
    }

    private sealed class LlamaCppChatResponse
    {
        [JsonPropertyName("choices")]
        public IReadOnlyList<LlamaCppChoice>? Choices { get; init; }
    }

    private sealed class LlamaCppChoice
    {
        [JsonPropertyName("message")]
        public LlamaCppResponseMessage? Message { get; init; }
    }

    private sealed class LlamaCppResponseMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }

        [JsonPropertyName("tool_calls")]
        public IReadOnlyList<LlamaCppToolCall>? ToolCalls { get; init; }
    }

    private sealed class LlamaCppToolCall
    {
        [JsonPropertyName("function")]
        public LlamaCppToolFunction? Function { get; init; }
    }

    private sealed class LlamaCppToolFunction
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("arguments")]
        public JsonElement Arguments { get; init; }
    }
}

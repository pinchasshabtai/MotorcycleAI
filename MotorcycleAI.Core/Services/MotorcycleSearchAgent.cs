using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MotorcycleAI.Core.Interfaces;
using MotorcycleAI.Core.Models;

namespace MotorcycleAI.Core.Services;

public sealed class MotorcycleSearchAgent : IMotorcycleSearchAgent
{
    private const string ToolName = "SearchMotorcycles";
    private const string SystemPrompt = """
        אתה סוכן חיפוש אופנועים. הפעל את הכלי SearchMotorcycles בדיוק פעם אחת.
        חלץ מהבקשה רק דרישות שהמשתמש ציין במפורש. אל תמציא תקציב, העדפות או מפרטים.
        השאר null בכל שדה שלא צוין במפורש. העתק שמות מותגים והעדפות כפי שנכתבו.
        """;

    private static readonly string[] RequiredProperties =
    [
        "VehicleType",
        "MinPrice",
        "MaxPrice",
        "MinEngineCC",
        "MaxEngineCC",
        "MinYear",
        "MaxYear",
        "RiderExperience",
        "Usage",
        "MotorcycleType",
        "PreferredBrands",
        "Reliability",
        "MaintenanceCost",
        "AdditionalPreferences"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private static readonly JsonElement SearchTools = CreateTools();

    private readonly IStructuredResponseGenerator _responseGenerator;
    private readonly IMotorcycleRepository _motorcycleRepository;
    private readonly ILogger<MotorcycleSearchAgent> _logger;

    public MotorcycleSearchAgent(
        IStructuredResponseGenerator responseGenerator,
        IMotorcycleRepository motorcycleRepository,
        ILogger<MotorcycleSearchAgent> logger)
    {
        _responseGenerator = responseGenerator;
        _motorcycleRepository = motorcycleRepository;
        _logger = logger;
    }

    public async Task<MotorcycleSearchResult> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var toolCalls = await _responseGenerator.GenerateToolCallsAsync(
            SystemPrompt,
            query,
            SearchTools,
            cancellationToken);

        if (toolCalls.Count != 1 || !string.Equals(toolCalls[0].Name, ToolName, StringComparison.Ordinal))
        {
            _logger.LogError(
                "The AI returned {ToolCallCount} tool calls or an unsupported tool name.",
                toolCalls.Count);
            throw new InvalidDataException("The AI must call SearchMotorcycles exactly once.");
        }

        var searchRequest = ParseSearchRequest(toolCalls[0].Arguments);
        var motorcycles = await _motorcycleRepository.SearchAsync(searchRequest, cancellationToken);
        return new MotorcycleSearchResult(searchRequest, motorcycles);
    }

    private MotorcycleSearchRequest ParseSearchRequest(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("The AI tool call arguments were not a JSON object.");
            throw new InvalidDataException("The AI tool call arguments must be a JSON object.");
        }

        var knownProperties = RequiredProperties.ToHashSet(StringComparer.Ordinal);
        var receivedProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in arguments.EnumerateObject())
        {
            if (!knownProperties.Contains(property.Name) || !receivedProperties.Add(property.Name))
            {
                _logger.LogWarning(
                    "The AI tool call contains an unknown or duplicate {Property} field.",
                    property.Name);
                throw new InvalidDataException("The AI tool call contains an unknown or duplicate field.");
            }
        }

        foreach (var property in RequiredProperties)
        {
            if (!receivedProperties.Contains(property))
            {
                _logger.LogWarning("The AI tool call is missing the {Property} field.", property);
                throw new InvalidDataException($"The AI tool call is missing the '{property}' field.");
            }
        }

        MotorcycleSearchRequest searchRequest;
        try
        {
            searchRequest = arguments.Deserialize<MotorcycleSearchRequest>(JsonOptions)
                ?? throw new InvalidDataException("The AI tool call could not be deserialized.");
            MotorcycleSearchRequestValidator.Validate(searchRequest);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "The AI returned invalid tool call arguments.");
            throw new InvalidDataException("The AI returned invalid tool call arguments.", exception);
        }
        catch (InvalidDataException exception)
        {
            _logger.LogWarning(exception, "The AI tool call arguments failed validation.");
            throw;
        }

        return searchRequest;
    }

    private static JsonElement CreateTools()
    {
        const string schemaJson = """
            {
              "type": "object",
              "properties": {
                "VehicleType": { "type": ["string", "null"] },
                "MinPrice": { "type": ["number", "null"], "minimum": 0 },
                "MaxPrice": { "type": ["number", "null"], "minimum": 0 },
                "MinEngineCC": { "type": ["integer", "null"], "minimum": 0, "maximum": 10000 },
                "MaxEngineCC": { "type": ["integer", "null"], "minimum": 0, "maximum": 10000 },
                "MinYear": { "type": ["integer", "null"], "minimum": 1885, "maximum": 2200 },
                "MaxYear": { "type": ["integer", "null"], "minimum": 1885, "maximum": 2200 },
                "RiderExperience": { "type": ["string", "null"] },
                "Usage": { "type": ["string", "null"] },
                "MotorcycleType": { "type": ["string", "null"] },
                "PreferredBrands": {
                  "type": ["array", "null"],
                  "items": { "type": "string" }
                },
                "Reliability": { "type": ["string", "null"] },
                "MaintenanceCost": { "type": ["string", "null"] },
                "AdditionalPreferences": { "type": ["string", "null"] }
              },
              "required": [
                "VehicleType",
                "MinPrice",
                "MaxPrice",
                "MinEngineCC",
                "MaxEngineCC",
                "MinYear",
                "MaxYear",
                "RiderExperience",
                "Usage",
                "MotorcycleType",
                "PreferredBrands",
                "Reliability",
                "MaintenanceCost",
                "AdditionalPreferences"
              ],
              "additionalProperties": false
            }
            """;

        var parameters = JsonNode.Parse(schemaJson)
            ?? throw new InvalidOperationException("The motorcycle search JSON Schema is invalid.");
        var yearProperties = parameters["properties"]
            ?? throw new InvalidOperationException("The motorcycle search JSON Schema has no properties.");
        foreach (var propertyName in new[] { "MinYear", "MaxYear" })
        {
            var yearSchema = yearProperties[propertyName]
                ?? throw new InvalidOperationException(
                    $"The motorcycle search JSON Schema is missing '{propertyName}'.");
            yearSchema["maximum"] = MotorcycleSearchRequestValidator.LatestMotorcycleYear;
        }

        var tool = new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = ToolName,
                ["description"] = "Search the motorcycle catalog using explicit user requirements.",
                ["parameters"] = parameters
            }
        };

        return JsonSerializer.SerializeToElement(new JsonArray(tool));
    }
}

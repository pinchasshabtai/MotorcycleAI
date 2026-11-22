using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MotorcycleAI.Core.Interfaces;
using MotorcycleAI.Core.Models;

namespace MotorcycleAI.Core.Services;

public sealed class SearchAnalyzer : ISearchAnalyzer
{
    private const string SystemPrompt = """
        נתח את בקשת המשתמש בעברית והחזר את דרישות החיפוש בלבד בהתאם למבנה ה-JSON Schema.
        אל תמציא תקציב, העדפות או מפרטים שלא צוינו. השאר null בכל שדה שלא צוין במפורש.
        העתק שמות מותגים והעדפות כפי שנכתבו. אל תסיק העדפה מתוך מידע כללי או מתוך היכרות עם אופנועים.
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

    private static readonly JsonElement SearchRequestSchema = CreateSchema();

    private readonly IStructuredResponseGenerator _responseGenerator;
    private readonly ILogger<SearchAnalyzer> _logger;

    public SearchAnalyzer(
        IStructuredResponseGenerator responseGenerator,
        ILogger<SearchAnalyzer> logger)
    {
        _responseGenerator = responseGenerator;
        _logger = logger;
    }

    public async Task<MotorcycleSearchRequest> AnalyzeAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var json = await _responseGenerator.GenerateAsync(
            SystemPrompt,
            query,
            SearchRequestSchema,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogError("The structured response generator returned an empty search request.");
            throw new InvalidDataException("The AI returned an empty search request.");
        }

        MotorcycleSearchRequest searchRequest;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("The AI search request must be a JSON object.");
            }

            foreach (var property in RequiredProperties)
            {
                if (!document.RootElement.TryGetProperty(property, out _))
                {
                    throw new InvalidDataException(
                        $"The AI search request is missing the '{property}' field.");
                }
            }

            searchRequest = document.RootElement.Deserialize<MotorcycleSearchRequest>(JsonOptions)
                ?? throw new InvalidDataException("The AI search request could not be deserialized.");
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "The AI returned invalid JSON for a search request.");
            throw new InvalidDataException("The AI returned invalid JSON for a search request.", exception);
        }
        catch (InvalidDataException exception)
        {
            _logger.LogWarning(exception, "The AI search request did not match the required structure.");
            throw;
        }

        try
        {
            MotorcycleSearchRequestValidator.Validate(searchRequest);
        }
        catch (InvalidDataException exception)
        {
            _logger.LogWarning(exception, "The AI search request failed validation.");
            throw;
        }

        return searchRequest;
    }

    private static JsonElement CreateSchema()
    {
        const string schemaJson = """
            {
              "type": "object",
              "properties": {
                "VehicleType": { "type": ["string", "null"] },
                "MinPrice": { "type": ["number", "null"] },
                "MaxPrice": { "type": ["number", "null"] },
                "MinEngineCC": { "type": ["integer", "null"] },
                "MaxEngineCC": { "type": ["integer", "null"] },
                "MinYear": { "type": ["integer", "null"] },
                "MaxYear": { "type": ["integer", "null"] },
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

        var schema = JsonNode.Parse(schemaJson)
            ?? throw new InvalidOperationException("The search request JSON Schema is invalid.");
        var properties = schema["properties"]
            ?? throw new InvalidOperationException("The search request JSON Schema has no properties.");

        foreach (var propertyName in new[] { "MinEngineCC", "MaxEngineCC" })
        {
            var engineSchema = properties[propertyName]
                ?? throw new InvalidOperationException(
                    $"The search request JSON Schema is missing '{propertyName}'.");
            engineSchema["minimum"] = 0;
            engineSchema["maximum"] = MotorcycleSearchRequestValidator.MaximumEngineCC;
        }

        foreach (var propertyName in new[] { "MinYear", "MaxYear" })
        {
            var yearSchema = properties[propertyName]
                ?? throw new InvalidOperationException(
                    $"The search request JSON Schema is missing '{propertyName}'.");
            yearSchema["minimum"] = MotorcycleSearchRequestValidator.EarliestMotorcycleYear;
            yearSchema["maximum"] = MotorcycleSearchRequestValidator.LatestMotorcycleYear;
        }

        return JsonSerializer.SerializeToElement(schema);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MotorcycleAI.Core.Interfaces;
using MotorcycleAI.Core.Models;

namespace MotorcycleAI.Core.Services;

public sealed class RecommendationService : IRecommendationService
{
    private const string SystemPrompt = """
        כתוב בעברית סיכום והסברים קצרים להמלצות על סמך המידע שסופק בלבד.
        לכל המלצה השתמש רק ב-supportedAdvantages וב-supportedDisadvantages כעובדות על התאמה.
        אל תמציא או תשנה מחיר, שנתון, נפח מנוע, מותג או מפרט.
        אל תוסיף אופנועים שאינם ברשימת המועמדים ואל תחשב או תציע ציון.
        החזר לכל MotorcycleId מועמד הסבר אחד. יתרונות וחסרונות ייקבעו על ידי הקוד בלבד.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonElement ExplanationSchema = CreateExplanationSchema();

    private readonly IStructuredResponseGenerator _responseGenerator;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        IStructuredResponseGenerator responseGenerator,
        ILogger<RecommendationService> logger)
    {
        _responseGenerator = responseGenerator;
        _logger = logger;
    }

    public async Task<RecommendationResult> RecommendAsync(
        string originalQuery,
        MotorcycleSearchRequest searchRequest,
        IReadOnlyList<Motorcycle> motorcycles,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalQuery);
        ArgumentNullException.ThrowIfNull(searchRequest);
        ArgumentNullException.ThrowIfNull(motorcycles);

        if (motorcycles.Count == 0)
        {
            return new RecommendationResult
            {
                Summary = "לא נמצאו אופנועים במאגר המתאימים לתנאי החיפוש."
            };
        }

        if (motorcycles.Select(motorcycle => motorcycle.Id).Distinct().Count() != motorcycles.Count)
        {
            throw new ArgumentException("Motorcycle IDs must be unique.", nameof(motorcycles));
        }

        var scoredMotorcycles = motorcycles
            .Select(motorcycle => ScoreMotorcycle(motorcycle, searchRequest))
            .OrderByDescending(scored => scored.MatchScore)
            .ThenBy(scored => scored.Motorcycle.Price)
            .ThenByDescending(scored => scored.Motorcycle.Year)
            .ToArray();

        var prompt = JsonSerializer.Serialize(
            new
            {
                OriginalQuery = originalQuery,
                SearchRequest = searchRequest,
                Candidates = scoredMotorcycles.Select(scored => new
                {
                    scored.Motorcycle.Id,
                    scored.Motorcycle.Manufacturer,
                    scored.Motorcycle.Model,
                    scored.Motorcycle.Year,
                    scored.Motorcycle.EngineCC,
                    scored.Motorcycle.Price,
                    scored.Motorcycle.VehicleType,
                    scored.Motorcycle.MotorcycleType,
                    scored.Motorcycle.RiderExperience,
                    scored.Motorcycle.Usage,
                    scored.Motorcycle.Reliability,
                    scored.Motorcycle.MaintenanceCost,
                    scored.Motorcycle.Description,
                    SupportedAdvantages = scored.Advantages,
                    SupportedDisadvantages = scored.Disadvantages
                })
            },
            JsonOptions);

        var responseJson = await _responseGenerator.GenerateAsync(
            SystemPrompt,
            prompt,
            ExplanationSchema,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            _logger.LogError("The AI returned an empty recommendation explanation response.");
            throw new InvalidDataException("The AI returned an empty recommendation explanation response.");
        }

        var explanationResponse = ParseAndValidateResponse(responseJson, scoredMotorcycles);
        var explanations = explanationResponse.Recommendations!.ToDictionary(
            recommendation => recommendation.MotorcycleId);

        return new RecommendationResult
        {
            Summary = explanationResponse.Summary!,
            Recommendations = scoredMotorcycles
                .Select(scored =>
                {
                    var explanation = explanations[scored.Motorcycle.Id];
                    return new Recommendation
                    {
                        MotorcycleId = scored.Motorcycle.Id,
                        MatchScore = scored.MatchScore,
                        Reason = explanation.Reason!,
                        Advantages = scored.Advantages,
                        Disadvantages = scored.Disadvantages
                    };
                })
                .ToArray()
        };
    }

    private RecommendationExplanationResponse ParseAndValidateResponse(
        string responseJson,
        IReadOnlyList<ScoredMotorcycle> scoredMotorcycles)
    {
        RecommendationExplanationResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<RecommendationExplanationResponse>(
                responseJson,
                JsonOptions);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "The AI returned invalid recommendation explanation JSON.");
            throw new InvalidDataException(
                "The AI returned invalid recommendation explanation JSON.",
                exception);
        }

        if (response is null || string.IsNullOrWhiteSpace(response.Summary) || response.Recommendations is null)
        {
            _logger.LogError("The AI recommendation response is missing its summary or recommendations.");
            throw new InvalidDataException(
                "The AI recommendation response is missing its summary or recommendations.");
        }

        var expectedIds = scoredMotorcycles
            .Select(scored => scored.Motorcycle.Id)
            .ToHashSet();
        var receivedIds = response.Recommendations
            .Select(recommendation => recommendation.MotorcycleId)
            .ToArray();

        if (receivedIds.Length != expectedIds.Count
            || receivedIds.Distinct().Count() != receivedIds.Length
            || !expectedIds.SetEquals(receivedIds))
        {
            _logger.LogError("The AI recommendation response contains missing, duplicate, or unknown motorcycle IDs.");
            throw new InvalidDataException(
                "The AI recommendation response must contain each candidate motorcycle exactly once.");
        }

        if (response.Recommendations.Any(recommendation =>
                string.IsNullOrWhiteSpace(recommendation.Reason)))
        {
            _logger.LogError("The AI recommendation response contains incomplete explanation fields.");
            throw new InvalidDataException(
                "The AI recommendation response contains incomplete explanation fields.");
        }

        return response;
    }

    private static ScoredMotorcycle ScoreMotorcycle(
        Motorcycle motorcycle,
        MotorcycleSearchRequest searchRequest)
    {
        var criteriaCount = 0;
        var matchedCriteria = 0;
        var advantages = new List<string>();
        var disadvantages = new List<string>();

        if (searchRequest.MinPrice is not null || searchRequest.MaxPrice is not null)
        {
            AddEvaluation(
                IsWithinRange(motorcycle.Price, searchRequest.MinPrice, searchRequest.MaxPrice),
                "המחיר עומד בטווח שהוגדר.",
                "המחיר אינו עומד בטווח שהוגדר.",
                ref criteriaCount,
                ref matchedCriteria,
                advantages,
                disadvantages);
        }

        if (searchRequest.MinEngineCC is not null || searchRequest.MaxEngineCC is not null)
        {
            AddEvaluation(
                IsWithinRange(motorcycle.EngineCC, searchRequest.MinEngineCC, searchRequest.MaxEngineCC),
                "נפח המנוע עומד בטווח שהוגדר.",
                "נפח המנוע אינו עומד בטווח שהוגדר.",
                ref criteriaCount,
                ref matchedCriteria,
                advantages,
                disadvantages);
        }

        if (searchRequest.MinYear is not null || searchRequest.MaxYear is not null)
        {
            AddEvaluation(
                IsWithinRange(motorcycle.Year, searchRequest.MinYear, searchRequest.MaxYear),
                "שנתון האופנוע עומד בטווח שהוגדר.",
                "שנתון האופנוע אינו עומד בטווח שהוגדר.",
                ref criteriaCount,
                ref matchedCriteria,
                advantages,
                disadvantages);
        }

        AddStringEvaluation(
            searchRequest.VehicleType,
            motorcycle.VehicleType,
            "סוג כלי הרכב",
            ref criteriaCount,
            ref matchedCriteria,
            advantages,
            disadvantages);
        AddStringEvaluation(
            searchRequest.MotorcycleType,
            motorcycle.MotorcycleType,
            "סוג האופנוע",
            ref criteriaCount,
            ref matchedCriteria,
            advantages,
            disadvantages);
        AddStringEvaluation(
            searchRequest.RiderExperience,
            motorcycle.RiderExperience,
            "רמת הניסיון",
            ref criteriaCount,
            ref matchedCriteria,
            advantages,
            disadvantages);
        AddStringEvaluation(
            searchRequest.Usage,
            motorcycle.Usage,
            "השימוש",
            ref criteriaCount,
            ref matchedCriteria,
            advantages,
            disadvantages);
        AddStringEvaluation(
            searchRequest.Reliability,
            motorcycle.Reliability,
            "האמינות",
            ref criteriaCount,
            ref matchedCriteria,
            advantages,
            disadvantages);
        AddStringEvaluation(
            searchRequest.MaintenanceCost,
            motorcycle.MaintenanceCost,
            "עלות התחזוקה",
            ref criteriaCount,
            ref matchedCriteria,
            advantages,
            disadvantages);

        if (searchRequest.PreferredBrands is { Count: > 0 })
        {
            var matchesBrand = searchRequest.PreferredBrands.Any(
                brand => string.Equals(
                    brand.Trim(),
                    motorcycle.Manufacturer.Trim(),
                    StringComparison.OrdinalIgnoreCase));
            AddEvaluation(
                matchesBrand,
                "היצרן תואם לאחד המותגים המועדפים.",
                "היצרן אינו אחד מהמותגים המועדפים.",
                ref criteriaCount,
                ref matchedCriteria,
                advantages,
                disadvantages);
        }

        var score = criteriaCount == 0
            ? 50m
            : decimal.Round(matchedCriteria * 100m / criteriaCount, 2, MidpointRounding.AwayFromZero);

        return new ScoredMotorcycle(motorcycle, score, advantages, disadvantages);
    }

    private static bool IsWithinRange<T>(T value, T? minimum, T? maximum)
        where T : struct, IComparable<T>
    {
        return (minimum is null || value.CompareTo(minimum.Value) >= 0)
            && (maximum is null || value.CompareTo(maximum.Value) <= 0);
    }

    private static void AddStringEvaluation(
        string? requestedValue,
        string? actualValue,
        string criterionName,
        ref int criteriaCount,
        ref int matchedCriteria,
        ICollection<string> advantages,
        ICollection<string> disadvantages)
    {
        if (string.IsNullOrWhiteSpace(requestedValue))
        {
            return;
        }

        var matches = actualValue is not null
            && string.Equals(
                requestedValue.Trim(),
                actualValue.Trim(),
                StringComparison.OrdinalIgnoreCase);
        AddEvaluation(
            matches,
            $"{criterionName} תואם לדרישה.",
            actualValue is null
                ? $"אין במאגר מידע על {criterionName}."
                : $"{criterionName} אינו תואם לדרישה.",
            ref criteriaCount,
            ref matchedCriteria,
            advantages,
            disadvantages);
    }

    private static void AddEvaluation(
        bool matches,
        string advantage,
        string disadvantage,
        ref int criteriaCount,
        ref int matchedCriteria,
        ICollection<string> advantages,
        ICollection<string> disadvantages)
    {
        criteriaCount++;
        if (matches)
        {
            matchedCriteria++;
            advantages.Add(advantage);
        }
        else
        {
            disadvantages.Add(disadvantage);
        }
    }

    private static JsonElement CreateExplanationSchema()
    {
        const string schemaJson = """
            {
              "type": "object",
              "properties": {
                "summary": { "type": "string" },
                "recommendations": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "motorcycleId": { "type": "integer" },
                      "reason": { "type": "string" }
                    },
                    "required": ["motorcycleId", "reason"],
                    "additionalProperties": false
                  }
                }
              },
              "required": ["summary", "recommendations"],
              "additionalProperties": false
            }
            """;

        using var document = JsonDocument.Parse(schemaJson);
        return document.RootElement.Clone();
    }

    private sealed record ScoredMotorcycle(
        Motorcycle Motorcycle,
        decimal MatchScore,
        IReadOnlyList<string> Advantages,
        IReadOnlyList<string> Disadvantages);

    private sealed class RecommendationExplanationResponse
    {
        [JsonPropertyName("summary")]
        public string? Summary { get; init; }

        [JsonPropertyName("recommendations")]
        public List<RecommendationExplanation>? Recommendations { get; init; }
    }

    private sealed class RecommendationExplanation
    {
        [JsonPropertyName("motorcycleId")]
        public int MotorcycleId { get; init; }

        [JsonPropertyName("reason")]
        public string? Reason { get; init; }
    }
}

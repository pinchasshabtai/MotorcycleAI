using MotorcycleAI.Core.Models;

namespace MotorcycleAI.Core.Services;

public static class MotorcycleSearchRequestValidator
{
    internal const int EarliestMotorcycleYear = 1885;
    internal const int MaximumEngineCC = 10_000;

    internal static int LatestMotorcycleYear => DateTime.UtcNow.Year + 1;

    public static void Validate(MotorcycleSearchRequest searchRequest)
    {
        ArgumentNullException.ThrowIfNull(searchRequest);

        ValidateNonNegative(searchRequest.MinPrice, nameof(searchRequest.MinPrice));
        ValidateNonNegative(searchRequest.MaxPrice, nameof(searchRequest.MaxPrice));
        ValidateRange(searchRequest.MinPrice, searchRequest.MaxPrice, nameof(searchRequest.MinPrice));

        ValidateEngineCC(searchRequest.MinEngineCC, nameof(searchRequest.MinEngineCC));
        ValidateEngineCC(searchRequest.MaxEngineCC, nameof(searchRequest.MaxEngineCC));
        ValidateRange(searchRequest.MinEngineCC, searchRequest.MaxEngineCC, nameof(searchRequest.MinEngineCC));

        ValidateYear(searchRequest.MinYear, nameof(searchRequest.MinYear));
        ValidateYear(searchRequest.MaxYear, nameof(searchRequest.MaxYear));
        ValidateRange(searchRequest.MinYear, searchRequest.MaxYear, nameof(searchRequest.MinYear));
    }

    private static void ValidateNonNegative(decimal? value, string fieldName)
    {
        if (value < 0)
        {
            throw new InvalidDataException($"{fieldName} cannot be negative.");
        }
    }

    private static void ValidateEngineCC(int? value, string fieldName)
    {
        if (value is < 0 or > MaximumEngineCC)
        {
            throw new InvalidDataException(
                $"{fieldName} must be between 0 and {MaximumEngineCC} cc.");
        }
    }

    private static void ValidateYear(int? value, string fieldName)
    {
        if (value is not null && (value < EarliestMotorcycleYear || value > LatestMotorcycleYear))
        {
            throw new InvalidDataException(
                $"{fieldName} must be between {EarliestMotorcycleYear} and next year.");
        }
    }

    private static void ValidateRange<T>(T? minimum, T? maximum, string fieldName)
        where T : struct, IComparable<T>
    {
        if (minimum is not null && maximum is not null && minimum.Value.CompareTo(maximum.Value) > 0)
        {
            throw new InvalidDataException($"{fieldName} cannot be greater than its maximum.");
        }
    }
}

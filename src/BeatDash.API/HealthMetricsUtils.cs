namespace Shiron.BeatDash.API;

/// <summary>
/// Validation and normalization for opt-in health/fitness body metadata. All values are
/// canonical metric; imperial conversion happens client-side before submission.
/// </summary>
public static class HealthMetricsUtils {
    public const string SexMale = "male";
    public const string SexFemale = "female";
    public const string SexUnspecified = "unspecified";

    private static readonly HashSet<string> AllowedSex = [SexMale, SexFemale, SexUnspecified];

    /// <summary>
    /// Normalizes a raw sex value to one of the allowed tokens, or null when blank.
    /// Returns the trimmed lowercase input unchanged when unrecognized so
    /// <see cref="Validate"/> can reject it with a clear message.
    /// </summary>
    public static string? NormalizeSex(string? raw) {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Validates the (already metric) health metadata. Returns the first human-readable error,
    /// or null when everything is within physiological range. Null fields are always valid —
    /// every metric is optional, and only weight is needed for the core calorie estimate.
    /// </summary>
    public static string? Validate(
        int? heightCm,
        double? weightKg,
        int? birthYear,
        string? sex,
        double? bodyFatPercent,
        int? restingHeartRate,
        int currentYear) {
        if (heightCm is < 90 or > 260)
            return "Height must be between 90 and 260 cm.";
        if (weightKg is < 20 or > 350 || (weightKg is { } w && !double.IsFinite(w)))
            return "Weight must be between 20 and 350 kg.";
        if (birthYear is { } by) {
            var age = currentYear - by;
            if (age is < 5 or > 120) return "Age must be between 5 and 120 years.";
        }
        if (sex is not null && !AllowedSex.Contains(sex))
            return "Sex must be male, female or unspecified.";
        if (bodyFatPercent is { } bf && (bf is < 3 or > 70 || !double.IsFinite(bf)))
            return "Body fat must be between 3% and 70%.";
        if (restingHeartRate is < 30 or > 120)
            return "Resting heart rate must be between 30 and 120 bpm.";
        return null;
    }
}

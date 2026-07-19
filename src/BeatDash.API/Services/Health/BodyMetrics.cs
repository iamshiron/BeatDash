namespace Shiron.BeatDash.API.Services.Health;

/// <summary>
/// Pure body-composition math derived from a user's metadata: BMI, lean mass, and BMR
/// (Katch-McArdle when body fat is known, else Mifflin-St Jeor). All inputs are metric.
/// Every function returns null when the inputs it needs are missing.
/// </summary>
public static class BodyMetrics {
    /// <summary>Body mass index (kg/m²).</summary>
    public static double? Bmi(int? heightCm, double? weightKg) =>
        heightCm is > 0 and { } h && weightKg is { } w
            ? w / Math.Pow(h / 100.0, 2)
            : null;

    /// <summary>Lean body mass (kg) from weight and body-fat percentage (0–100).</summary>
    public static double? LeanMassKg(double? weightKg, double? bodyFatPercent) =>
        weightKg is { } w && bodyFatPercent is { } bf
            ? w * (1 - bf / 100.0)
            : null;

    /// <summary>
    /// Basal metabolic rate (kcal/day): Katch-McArdle when body fat is known (most accurate),
    /// otherwise Mifflin-St Jeor (needs age + sex), otherwise null.
    /// </summary>
    public static double? Bmr(double? weightKg, int? heightCm, int? age, string? sex, double? bodyFatPercent) {
        if (LeanMassKg(weightKg, bodyFatPercent) is { } lbm)
            return 370 + 21.6 * lbm;

        if (weightKg is { } w && heightCm is { } h && age is { } a) {
            var baseVal = 10 * w + 6.25 * h - 5 * a;
            return sex switch {
                "male" => baseVal + 5,
                "female" => baseVal - 161,
                _ => baseVal + (5 - 161) / 2.0 // unspecified: midpoint of the two offsets
            };
        }
        return null;
    }
}

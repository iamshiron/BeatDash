namespace Shiron.BeatDash.API.Services.Health;

/// <summary>How a calorie figure was derived, best (most trustworthy) first.</summary>
public enum CalorieConfidence {
    /// <summary>Difficulty/NPS heuristic — no motion or heart-rate data.</summary>
    Estimated,
    /// <summary>Motion-modulated MET from tracked saber/head movement.</summary>
    Motion,
    /// <summary>Heart-rate based (Keytel) — the most accurate tier.</summary>
    Hr
}

/// <summary>Result of a per-play energy-expenditure estimate.</summary>
public readonly record struct CalorieEstimate(
    double Kcal,
    double ActiveMinutes,
    double? Met,
    double? AvgHr,
    double Intensity01,
    CalorieConfidence Confidence);

/// <summary>
/// Pure, DB-free energy-expenditure estimator for a Beat Saber play. Picks the best available
/// tier: heart-rate (Keytel 2005) → motion-modulated MET → difficulty/NPS fallback. Weight is
/// the only always-required input. Mirrors <c>MotionSummaryCalculator</c>'s pure-static shape so
/// it can be unit-tested in isolation.
/// </summary>
public static class CalorieEstimator {
    // Beat Saber MET band (VR Institute of Health & Exercise baseline ≈ 6.24, mid-band).
    private const double MetMin = 4.0;
    private const double MetMax = 9.0;
    // Mean saber speed (m/s over song span) normalization bounds.
    private const double SpeedLow = 0.5;
    private const double SpeedHigh = 3.0;
    // Head travel per minute (m/min) that reads as very active (whole-body movement).
    private const double HeadTravelPerMinHigh = 30.0;
    // NPS normalization bounds for the difficulty-only fallback.
    private const double NpsLow = 2.0;
    private const double NpsHigh = 10.0;

    /// <summary>
    /// Estimate calories for one play. <paramref name="avgSaberSpeed"/> / <paramref name="headTravel"/>
    /// come from the play's motion summary (null when it had no motion data);
    /// <paramref name="avgHr"/> is the mean heart rate from wearable samples overlapping the play
    /// (null when none). HR tier also needs <paramref name="age"/> and a binary <paramref name="sex"/>.
    /// </summary>
    public static CalorieEstimate Estimate(
        double weightKg,
        int endSongTimeMs,
        double? avgSaberSpeed,
        double? headTravel,
        float notesPerSecond,
        double? avgHr,
        int? age,
        string? sex) {
        var minutes = Math.Max(0, endSongTimeMs) / 60000.0;

        // Tier 1 — heart rate (Keytel). Needs HR + age + a male/female formula.
        if (avgHr is { } hr && age is { } a && IsBinarySex(sex)) {
            var perMin = Math.Max(0, KeytelKcalPerMinute(hr, weightKg, a, sex!));
            var intensity = Clamp01((hr - 60.0) / (180.0 - 60.0));
            return new CalorieEstimate(perMin * minutes, minutes, null, hr, intensity, CalorieConfidence.Hr);
        }

        // Tier 2 — motion-modulated MET.
        if (avgSaberSpeed is { } speed) {
            var speedNorm = Clamp01((speed - SpeedLow) / (SpeedHigh - SpeedLow));
            var headNorm = headTravel is { } ht && minutes > 0
                ? Clamp01(ht / minutes / HeadTravelPerMinHigh)
                : 0;
            var intensity = Clamp01(0.8 * speedNorm + 0.2 * headNorm);
            var met = MetMin + intensity * (MetMax - MetMin);
            return new CalorieEstimate(met * weightKg * (minutes / 60.0), minutes, met, avgHr, intensity, CalorieConfidence.Motion);
        }

        // Tier 3 — difficulty-only fallback from NPS.
        var npsNorm = Clamp01((notesPerSecond - NpsLow) / (NpsHigh - NpsLow));
        var estMet = MetMin + npsNorm * (MetMax - MetMin);
        return new CalorieEstimate(estMet * weightKg * (minutes / 60.0), minutes, estMet, null, npsNorm, CalorieConfidence.Estimated);
    }

    /// <summary>Keytel et al. (2005) heart-rate energy expenditure, kcal/min.</summary>
    private static double KeytelKcalPerMinute(double hr, double weightKg, int age, string sex) =>
        sex == "female"
            ? (-20.4022 + 0.4472 * hr - 0.1263 * weightKg + 0.0740 * age) / 4.184
            : (-55.0969 + 0.6309 * hr + 0.1988 * weightKg + 0.2017 * age) / 4.184;

    private static bool IsBinarySex(string? sex) => sex is "male" or "female";

    private static double Clamp01(double v) => Math.Clamp(v, 0, 1);
}

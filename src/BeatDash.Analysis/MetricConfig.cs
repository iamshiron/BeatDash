using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Shiron.BeatDash.Analysis;

/// <summary>
/// A linear model over features: <c>bias + Σ (weight · feature)</c>, optionally
/// normalized to <c>[0,1]</c> by dividing by <see cref="Scale"/> and clamping.
/// This is the calibration surface — tuning a metric means changing these numbers,
/// not code. All members are settable so the whole thing binds from configuration.
/// </summary>
public sealed class WeightedModel {
    /// <summary>Feature key → weight. Missing features contribute nothing.</summary>
    public Dictionary<string, double> Weights { get; set; } = [];

    /// <summary>Constant added to the weighted sum.</summary>
    public double Bias { get; set; }

    /// <summary>Raw value that maps to <c>1.0</c> after normalization. Must be &gt; 0.</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>The raw (unnormalized) weighted sum.</summary>
    public double Evaluate(IReadOnlyDictionary<string, double> features) {
        var sum = Bias;
        foreach (var (key, weight) in Weights) {
            if (features.TryGetValue(key, out var value)) sum += weight * value;
        }
        return sum;
    }

    /// <summary>The weighted sum divided by <see cref="Scale"/>, clamped to <c>[0,1]</c>.</summary>
    public double Normalized(IReadOnlyDictionary<string, double> features) =>
        Math.Clamp(Evaluate(features) / (Scale <= 0 ? 1.0 : Scale), 0.0, 1.0);
}

/// <summary>PP curve: <c>pp = Multiplier · difficulty^Exponent</c>.</summary>
public sealed class PpConfig {
    public double Multiplier { get; set; } = 500.0;
    public double Exponent { get; set; } = 2.5;
}

/// <summary>
/// The full calibration for the metric layer. Bind a "Metrics" configuration section
/// over <see cref="CreateDefault"/> to recalibrate without recompiling.
///
/// <para><b>The default weights below are provisional and uncalibrated</b> — they
/// produce self-consistent, sanely-ranged values, not corpus-anchored ones. The
/// corpus-fitting pass replaces these numbers.</para>
/// </summary>
public sealed class MetricConfig {
    public WeightedModel Difficulty { get; set; } = new();
    public PpConfig Pp { get; set; } = new();
    public Dictionary<string, WeightedModel> Characteristics { get; set; } = [];

    /// <summary>
    /// A short, stable fingerprint of this calibration. Changes whenever any weight,
    /// scale, multiplier or exponent changes — used to detect when stored metrics
    /// need recomputing.
    /// </summary>
    public string Fingerprint() {
        var json = JsonSerializer.Serialize(this);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash)[..16];
    }

    public static MetricConfig CreateDefault() => new() {
        Difficulty = new WeightedModel {
            Weights = {
                ["density.nps"] = 0.6,
                ["swing.hand_speed_p95"] = 0.25,
                ["density.burstiness"] = 1.5,
                ["swing.reset_rate"] = 5.0,
            },
            Scale = 20.0,
        },
        Pp = new PpConfig { Multiplier = 500.0, Exponent = 2.5 },
        Characteristics = new Dictionary<string, WeightedModel> {
            // Stream — sustained, steady density; penalize burstiness and rest.
            ["stream"] = new WeightedModel {
                Weights = {
                    ["density.nps_median_1s"] = 1.0,
                    ["density.nps"] = 0.4,
                    ["density.burstiness"] = -1.0,
                    ["density.rest_ratio"] = -3.0,
                },
                Bias = 2.0,
                Scale = 12.0,
            },
            // Tech — resets, dots, crossovers, sliders, sharp angle changes.
            ["tech"] = new WeightedModel {
                Weights = {
                    ["swing.reset_rate"] = 4.0,
                    ["pattern.dot_ratio"] = 1.5,
                    ["swing.crossover_rate"] = 1.0,
                    ["density.slider_nps"] = 1.5,
                    ["swing.angle_change_mean"] = 0.003,
                },
                Scale = 3.0,
            },
            // Speed — required saber velocity and travel distance.
            ["speed"] = new WeightedModel {
                Weights = {
                    ["swing.hand_speed_p95"] = 0.06,
                    ["swing.hand_speed_max"] = 0.03,
                    ["timing.jump_distance"] = 0.02,
                },
                Scale = 2.5,
            },
            // Jumps — spiky bursts rather than sustained density.
            ["jumps"] = new WeightedModel {
                Weights = {
                    ["density.burstiness"] = 0.6,
                    ["density.nps_peak_1s"] = 0.15,
                },
                Scale = 4.0,
            },
            // Gimmick — bombs, walls, dot-spam.
            ["gimmick"] = new WeightedModel {
                Weights = {
                    ["density.bomb_nps"] = 3.0,
                    ["count.obstacles"] = 0.02,
                    ["pattern.dot_ratio"] = 1.0,
                },
                Scale = 3.0,
            },
        },
    };
}

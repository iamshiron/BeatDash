using Shiron.BeatDash.API.Services.Health;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class CalorieEstimatorTests {
    [Fact]
    public void Estimate_CompleteHeartRateInputs_PrefersHeartRateTier() {
        var result = CalorieEstimator.Estimate(70, 600_000, 2, 100, 6, 150, 30, "male");

        Assert.Equal(CalorieConfidence.Hr, result.Confidence);
        Assert.Null(result.Met);
        Assert.Equal(150, result.AvgHr);
        Assert.Equal(10, result.ActiveMinutes);
        Assert.True(result.Kcal > 0);
        Assert.InRange(result.Intensity01, 0, 1);
    }

    [Fact]
    public void Estimate_WithoutHeartRate_UsesMotionTier() {
        var result = CalorieEstimator.Estimate(70, 600_000, 3, 300, 6, null, null, null);

        Assert.Equal(CalorieConfidence.Motion, result.Confidence);
        Assert.Equal(9, result.Met);
        Assert.Null(result.AvgHr);
        Assert.Equal(1, result.Intensity01);
        Assert.Equal(105, result.Kcal);
    }

    [Fact]
    public void Estimate_WithoutMotionOrHeartRate_UsesDifficultyEstimate() {
        var result = CalorieEstimator.Estimate(60, 300_000, null, null, 6, null, null, null);

        Assert.Equal(CalorieConfidence.Estimated, result.Confidence);
        Assert.Equal(6.5, result.Met);
        Assert.Null(result.AvgHr);
        Assert.Equal(0.5, result.Intensity01);
        Assert.Equal(32.5, result.Kcal);
    }
}

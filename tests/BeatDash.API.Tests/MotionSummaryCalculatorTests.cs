using System.Text.Json;
using Shiron.BeatDash.API.Services.Motion;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class MotionSummaryCalculatorTests {
    [Fact]
    public void Compute_MovingSabersAndHead_CalculatesTravelSpeedRangeAndFatigue() {
        var samples = new float[42];
        SetPosition(samples, 0, leftX: 0, rightX: 0, headY: 0);
        SetPosition(samples, 1, leftX: 3, rightX: 4, headY: 12);

        var result = MotionSummaryCalculator.Compute(samples, 2, 2, 2000, Guid.NewGuid());
        var fatigue = JsonSerializer.Deserialize<MotionFatigueSample[]>(result.FatigueCurve);

        Assert.Equal(2, result.FrameCount);
        Assert.Equal(3, result.LeftSaberTravel);
        Assert.Equal(4, result.RightSaberTravel);
        Assert.Equal(12, result.HeadTravel);
        Assert.Equal(1.5, result.AvgLeftSaberSpeed);
        Assert.Equal(2, result.AvgRightSaberSpeed);
        Assert.Equal(3, result.LeftReachRange);
        Assert.Equal(4, result.RightReachRange);
        Assert.Equal(12, result.HeadRange);
        Assert.NotNull(fatigue);
        Assert.Equal(2, fatigue.Length);
        Assert.Equal(0, fatigue[0].LeftSpeed);
        Assert.Equal(0, fatigue[0].RightSpeed);
        Assert.Equal(3, fatigue[1].LeftSpeed);
        Assert.Equal(4, fatigue[1].RightSpeed);
    }

    [Fact]
    public void Compute_NoFrames_ReturnsZeroedSummaryAndEmptyFatigueCurve() {
        var result = MotionSummaryCalculator.Compute([], 0, 10, 0, Guid.NewGuid());
        var fatigue = JsonSerializer.Deserialize<MotionFatigueSample[]>(result.FatigueCurve);

        Assert.Equal(0, result.FrameCount);
        Assert.Equal(0, result.LeftSaberTravel);
        Assert.Equal(0, result.AvgLeftSaberSpeed);
        Assert.Equal(0, result.LeftReachRange);
        Assert.NotNull(fatigue);
        Assert.Empty(fatigue);
    }

    private static void SetPosition(float[] samples, int frame, float leftX, float rightX, float headY) {
        var offset = frame * 21;
        samples[offset] = leftX;
        samples[offset + 7] = rightX;
        samples[offset + 15] = headY;
    }
}

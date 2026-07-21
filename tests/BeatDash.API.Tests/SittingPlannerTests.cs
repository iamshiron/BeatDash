using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.API.Services;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public class SittingPlannerTests {
    private static readonly DateTime Base = new(2026, 7, 21, 18, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Gap = TimeSpan.FromMinutes(45);

    /// <summary>A play starting <paramref name="startMinute"/> minutes after the base, lasting <paramref name="lengthMinutes"/>.</summary>
    private static PlayInstant Play(int startMinute, int lengthMinutes = 4) {
        var start = Base.AddMinutes(startMinute);
        return new PlayInstant(Guid.NewGuid(), start, start.AddMinutes(lengthMinutes));
    }

    [Fact]
    public void Group_EmptyTimeline_ReturnsNoSittings() {
        var sittings = SittingPlanner.Group([], Gap);
        Assert.Empty(sittings);
    }

    [Fact]
    public void Group_ContiguousPlays_FormsSingleSitting() {
        var timeline = new[] { Play(0), Play(6), Play(12) };

        var sittings = SittingPlanner.Group(timeline, Gap);

        Assert.Single(sittings);
        Assert.Equal(3, sittings[0].PlayCount);
        Assert.Equal(timeline[0].StartedAt, sittings[0].StartedAt);
        Assert.Equal(timeline[2].EndedAt, sittings[0].EndedAt);
    }

    [Fact]
    public void Group_GapBeyondThreshold_SplitsIntoTwoSittings() {
        // Second cluster starts 46 min after the first play's END (> 45 min gap).
        var timeline = new[] { Play(0), Play(6), Play(6 + 4 + 46), Play(6 + 4 + 52) };

        var sittings = SittingPlanner.Group(timeline, Gap);

        Assert.Equal(2, sittings.Count);
        Assert.Equal(2, sittings[0].PlayCount);
        Assert.Equal(2, sittings[1].PlayCount);
    }

    [Fact]
    public void Group_GapExactlyThreshold_StaysInSameSitting() {
        // Next play starts exactly 45 min after the previous end — boundary is inclusive.
        var timeline = new[] { Play(0), Play(4 + 45) };

        var sittings = SittingPlanner.Group(timeline, Gap);

        Assert.Single(sittings);
        Assert.Equal(2, sittings[0].PlayCount);
    }

    [Fact]
    public void Sort_Newest_OrdersByStartDescending() {
        var older = new Sitting([Guid.NewGuid()], Base, Base.AddMinutes(10));
        var newer = new Sitting([Guid.NewGuid()], Base.AddHours(3), Base.AddHours(3).AddMinutes(10));

        var sorted = SittingPlanner.Sort([older, newer], SittingSortBy.Newest);

        Assert.Equal(newer.StartedAt, sorted[0].StartedAt);
        Assert.Equal(older.StartedAt, sorted[1].StartedAt);
    }

    [Fact]
    public void Sort_Oldest_OrdersByStartAscending() {
        var older = new Sitting([Guid.NewGuid()], Base, Base.AddMinutes(10));
        var newer = new Sitting([Guid.NewGuid()], Base.AddHours(3), Base.AddHours(3).AddMinutes(10));

        var sorted = SittingPlanner.Sort([newer, older], SittingSortBy.Oldest);

        Assert.Equal(older.StartedAt, sorted[0].StartedAt);
        Assert.Equal(newer.StartedAt, sorted[1].StartedAt);
    }

    [Fact]
    public void Sort_MostPlays_OrdersByPlayCountDescending() {
        var few = new Sitting([Guid.NewGuid()], Base, Base.AddMinutes(10));
        var many = new Sitting([Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()], Base.AddHours(1), Base.AddHours(1).AddMinutes(30));

        var sorted = SittingPlanner.Sort([few, many], SittingSortBy.MostPlays);

        Assert.Equal(3, sorted[0].PlayCount);
        Assert.Equal(1, sorted[1].PlayCount);
    }

    [Fact]
    public void Sort_Longest_OrdersByDurationDescending() {
        var shortSitting = new Sitting([Guid.NewGuid()], Base, Base.AddMinutes(5));
        var longSitting = new Sitting([Guid.NewGuid()], Base.AddHours(1), Base.AddHours(1).AddMinutes(90));

        var sorted = SittingPlanner.Sort([shortSitting, longSitting], SittingSortBy.Longest);

        Assert.Equal(longSitting.StartedAt, sorted[0].StartedAt);
        Assert.Equal(shortSitting.StartedAt, sorted[1].StartedAt);
    }
}

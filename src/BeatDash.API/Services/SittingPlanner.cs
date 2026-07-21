using Shiron.BeatDash.API.Endpoints;

namespace Shiron.BeatDash.API.Services;

/// <summary>One entry in a play timeline: a play id with its start/end instants.</summary>
public readonly record struct PlayInstant(Guid Id, DateTime StartedAt, DateTime EndedAt);

/// <summary>
/// A contiguous cluster of plays with no long idle gap between them — a "sitting" in the
/// player-facing sense. Carries only the lightweight timeline data needed to order and page
/// sittings before the per-page plays are hydrated.
/// </summary>
public sealed record Sitting(IReadOnlyList<Guid> PlayIds, DateTime StartedAt, DateTime EndedAt) {
    /// <summary>Number of plays in the sitting.</summary>
    public int PlayCount => PlayIds.Count;

    /// <summary>Wall-clock span from the first play's start to the last play's end.</summary>
    public TimeSpan Duration => EndedAt - StartedAt;
}

/// <summary>
/// Pure grouping, ordering and aggregation of a play timeline into sittings. Kept free of
/// EF/DB concerns so the sitting logic is unit-testable in isolation.
/// </summary>
public static class SittingPlanner {
    /// <summary>
    /// Groups a chronological play timeline into sittings, starting a new sitting whenever the
    /// idle gap before a play exceeds <paramref name="gap"/>. Input must be ordered ascending by
    /// <see cref="PlayInstant.StartedAt"/>; sittings are returned in the same chronological order.
    /// </summary>
    /// <param name="timeline">Plays ordered oldest-first.</param>
    /// <param name="gap">Idle gap that splits one sitting from the next.</param>
    public static List<Sitting> Group(IReadOnlyList<PlayInstant> timeline, TimeSpan gap) {
        var sittings = new List<Sitting>();
        if (timeline.Count == 0) return sittings;

        var ids = new List<Guid> { timeline[0].Id };
        var start = timeline[0].StartedAt;
        var prevEnd = timeline[0].EndedAt;

        for (var i = 1; i < timeline.Count; i++) {
            var t = timeline[i];
            if (t.StartedAt - prevEnd > gap) {
                sittings.Add(new Sitting(ids, start, prevEnd));
                ids = [];
                start = t.StartedAt;
            }
            ids.Add(t.Id);
            prevEnd = t.EndedAt;
        }
        sittings.Add(new Sitting(ids, start, prevEnd));
        return sittings;
    }

    /// <summary>
    /// Orders sittings by the requested key. Every key except <see cref="SittingSortBy.Oldest"/>
    /// breaks ties on most-recent-first so ordering is deterministic.
    /// </summary>
    public static IReadOnlyList<Sitting> Sort(IReadOnlyList<Sitting> sittings, SittingSortBy sortBy) =>
        sortBy switch {
            SittingSortBy.Oldest => sittings.OrderBy(s => s.StartedAt).ToList(),
            SittingSortBy.MostPlays => sittings
                .OrderByDescending(s => s.PlayCount).ThenByDescending(s => s.StartedAt).ToList(),
            SittingSortBy.Longest => sittings
                .OrderByDescending(s => s.Duration).ThenByDescending(s => s.StartedAt).ToList(),
            _ => sittings.OrderByDescending(s => s.StartedAt).ToList(),
        };
}

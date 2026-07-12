using System.ComponentModel.DataAnnotations;

namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// A per-user, incrementally-maintained rollup of note outcomes, folded once per
/// completed play session (see <c>WeaknessAggregationService</c>). Keyed at the
/// finest granularity the lifetime-weakness views need — game-mode characteristic
/// × hand × cut direction × grid cell — so the cut-direction matrix and the grid
/// heatmap are both marginals of the same joint table. All value columns are
/// additive running sums, making folds a cheap additive upsert.
/// </summary>
public sealed class PlayNoteAggregate {
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    /// <summary>Game-mode characteristic (Standard, OneSaber, …), not the analysis vector.</summary>
    [MaxLength(64)] public required string CharacteristicSerializedName { get; set; }

    /// <summary>Saber/hand: <see cref="ColorType.A"/> or <see cref="ColorType.B"/> (bombs excluded).</summary>
    public required ColorType ColorType { get; set; }

    /// <summary>Real swing direction (<c>0–7</c>); <see cref="CutDirection.Any"/>/<see cref="CutDirection.None"/> excluded.</summary>
    public required CutDirection CutDirection { get; set; }

    /// <summary>Grid column (<c>0–3</c>).</summary>
    public required int LineIndex { get; set; }

    /// <summary>Grid row (<c>0–2</c>).</summary>
    public required int NoteLineLayer { get; set; }

    /// <summary>Total notes seen at this key (good + bad + missed).</summary>
    public long NoteCount { get; set; }

    /// <summary>Good cuts (<c>Result == 0</c>).</summary>
    public long GoodCount { get; set; }

    /// <summary>Missed notes (<c>Result == 2</c>).</summary>
    public long MissCount { get; set; }

    /// <summary>Bad cuts (<c>Result == 1</c>).</summary>
    public long BadCount { get; set; }

    /// <summary>Earned score (before + center + after) summed over good cuts only.</summary>
    public long SumEarnedScore { get; set; }

    /// <summary>Achievable score summed over good cuts only. Cut accuracy = <see cref="SumEarnedScore"/> / this.</summary>
    public long SumMaxScore { get; set; }
}

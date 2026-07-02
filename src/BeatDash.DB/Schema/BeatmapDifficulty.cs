using System.ComponentModel.DataAnnotations;

namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// A specific difficulty/characteristic variant of a <see cref="Beatmap"/>,
/// holding the gameplay stats reported per play.
/// </summary>
public sealed class BeatmapDifficulty {
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required BeatmapDifficultyRank DifficultyRank { get; set; }

    /// <summary>
    /// The custom difficulty label (e.g. <c>"Nightmare"</c>), falling back to the
    /// rank name when unset.
    /// </summary>
    [MaxLength(64)] public required string DifficultyName { get; set; }

    public required float NotesPerSecond { get; set; }
    public required int CuttableObjectCount { get; set; }
    public required int BombCount { get; set; }
    public required int ObstacleCount { get; set; }
    public required int LaneCount { get; set; }
    public float? NoteJumpSpeed { get; set; }

    // Characteristic (game mode) — folded in, derivable per SerializedName.
    [MaxLength(64)] public required string CharacteristicSerializedName { get; set; }
    public required int CharacteristicColorCount { get; set; }
    public required bool CharacteristicRequires360Movement { get; set; }
    public required bool CharacteristicContainsRotationEvents { get; set; }

    /// <summary>
    /// The user who first submitted this difficulty variant. Audit only;
    /// nulled if the user is removed.
    /// </summary>
    public Guid? SubmittedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid BeatmapId { get; set; }
    public Beatmap Beatmap { get; set; } = null!;
}

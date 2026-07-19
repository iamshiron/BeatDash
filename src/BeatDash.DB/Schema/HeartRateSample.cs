using System.ComponentModel.DataAnnotations;

namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// A single heart-rate (and optional other-metric) sample pushed by a user's wearable /
/// companion app — data the Beat Saber client cannot measure. Associated to plays on read by
/// matching <see cref="RecordedAt"/> to a session's time window. Cascade-deleted with the user.
/// </summary>
public sealed class HeartRateSample {
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>When the sample was measured (UTC).</summary>
    public DateTime RecordedAt { get; set; }

    /// <summary>Heart rate in beats per minute.</summary>
    public int Bpm { get; set; }

    /// <summary>Optional watch-reported active calories for the interval.</summary>
    public double? CaloriesKcal { get; set; }

    /// <summary>Optional step count for the interval.</summary>
    public int? Steps { get; set; }

    /// <summary>Free-form source label (e.g. device/app name).</summary>
    [MaxLength(64)] public string? Source { get; set; }
}

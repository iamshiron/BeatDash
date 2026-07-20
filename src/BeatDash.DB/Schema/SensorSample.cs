using System.ComponentModel.DataAnnotations;

namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// A single generalized sensor reading pushed by a user's wearable / companion app (the Honami
/// Sensor Proxy) — data the Beat Saber client cannot measure. Each row is one metric at one
/// instant; associated to plays on read by matching <see cref="RecordedAt"/> to a session's time
/// window. Cascade-deleted with the user.
/// </summary>
public sealed class SensorSample {
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Canonical metric name, e.g. <c>heart_rate</c>, <c>calories</c>, <c>steps</c>.</summary>
    [MaxLength(48)] public required string Metric { get; set; }

    /// <summary>The reading, in the metric's canonical unit (bpm / kcal / count / percent).</summary>
    public double Value { get; set; }

    /// <summary>Optional unit label as sent by the client; canonical unit is implied by <see cref="Metric"/>.</summary>
    [MaxLength(16)] public string? Unit { get; set; }

    /// <summary>When the sample was measured (UTC).</summary>
    public DateTime RecordedAt { get; set; }

    /// <summary>Free-form source label (e.g. device/app name).</summary>
    [MaxLength(64)] public string? Source { get; set; }
}

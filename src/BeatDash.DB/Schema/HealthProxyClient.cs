using System.ComponentModel.DataAnnotations;

namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// A linked Honami Sensor Proxy client (a wearable / companion app) authorized to push sensor
/// samples for a user. Each client owns its own scoped token — only its SHA-256 hash is stored —
/// so multiple clients can push concurrently and be revoked independently. Cascade-deleted with
/// the user.
/// </summary>
public sealed class HealthProxyClient {
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>User-facing label, e.g. "Galaxy Watch".</summary>
    [MaxLength(64)] public required string Name { get; set; }

    /// <summary>SHA-256 hash (base64) of the client's push token; the plaintext is shown once.</summary>
    [MaxLength(128)] public required string TokenHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When this client last pushed a sample; null until it first connects.</summary>
    public DateTime? LastSeenAt { get; set; }
}

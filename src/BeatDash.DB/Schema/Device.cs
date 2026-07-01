using System.ComponentModel.DataAnnotations;

namespace Shiron.BeatDash.DB.Schema;

public class Device {
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ClientId { get; set; }

    [MaxLength(32)] public required string Name { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Guid UserId { get; set; }

    public IList<RefreshToken> RefreshTokens { get; set; } = [];
}

using System.ComponentModel.DataAnnotations;

namespace Shiron.BeatDash.DB.Schema;

public class RefreshToken {
    public Guid Id { get; set; } = Guid.CreateVersion7();
    [MaxLength(128)] public required string Token { get; set; }
    public required DateTime Expires { get; set; }
    public DateTime? RevokedAt { get; set; }

    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
}

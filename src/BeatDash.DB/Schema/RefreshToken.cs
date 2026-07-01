namespace Shiron.BeatDash.DB.Schema;

public class RefreshToken {
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public bool Revoked { get; set; } = false;
    public required string Token { get; set; }
    public required DateTime Expires { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}

namespace Shiron.BeatDash.DB.Schema;

public class AuthTokenSession {
    public Guid ID { get; set; } = Guid.CreateVersion7();
    public required string Token { get; set; }
    public DateTime Expires { get; set; }
    public bool Revoked { get; set; } = false;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}

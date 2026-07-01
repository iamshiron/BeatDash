using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Shiron.BeatDash.DB.Schema;

public class User : IdentityUser<Guid> {
    [MaxLength(32)] public required string DisplayName { get; set; }
    [MaxLength(32)] public override string? UserName { get; set; }

    public IList<RefreshToken> RefreshTokens { get; set; } = [];
}

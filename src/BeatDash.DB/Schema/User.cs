using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Shiron.BeatDash.DB.Schema;

public class User : IdentityUser<Guid> {
    [MaxLength(32)] public required string DisplayName { get; set; }
    [MaxLength(32)] public override string? UserName { get; set; }

    /// <summary>
    /// Public, shareable handle used in profile links (<c>/u/@handle</c>). Normalized to
    /// lowercase <c>[a-z0-9_]</c>, 3–32 chars, unique. Null until the user opts in.
    /// </summary>
    [MaxLength(32)] public string? Handle { get; set; }

    // Per-section public-profile visibility. All default false — nothing is exposed
    // until the user explicitly opts a section in.
    public bool ProfileStatsPublic { get; set; }
    public bool ProfileActivityPublic { get; set; }
    public bool ProfileSkillPublic { get; set; }
    public bool ProfileHistoryPublic { get; set; }
    public bool ProfileListsPublic { get; set; }
    public bool ProfileLikedPublic { get; set; }

    /// <summary>
    /// Object-storage keys for the user's uploaded avatar and profile banner, or null
    /// when unset. The extension encodes the image type (e.g. <c>avatars/{id}.png</c>).
    /// </summary>
    [MaxLength(256)] public string? AvatarKey { get; set; }
    [MaxLength(256)] public string? BannerKey { get; set; }

    public IList<Device> Devices { get; set; } = [];
}

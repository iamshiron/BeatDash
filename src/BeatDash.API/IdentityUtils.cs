using System.Security.Claims;

namespace Shiron.BeatDash.API;

public static class IdentityUtils {
    public static Guid? GetUserID(ClaimsPrincipal principal) {
        var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return sub is not null ? Guid.Parse(sub) : null;
    }
}

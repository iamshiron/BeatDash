using System.Text.RegularExpressions;

namespace Shiron.BeatDash.API;

/// <summary>
/// Normalization and validation for public profile handles used in <c>/u/@handle</c> links.
/// </summary>
public static partial class HandleUtils {
    [GeneratedRegex("^[a-z0-9_]{3,32}$")]
    private static partial Regex HandlePattern();

    /// <summary>
    /// Normalizes a raw handle for storage/lookup: strips a leading <c>@</c>, trims, and
    /// lowercases. Returns null when the input is null or blank.
    /// </summary>
    public static string? Normalize(string? raw) {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim().TrimStart('@').Trim();
        return trimmed.Length == 0 ? null : trimmed.ToLowerInvariant();
    }

    /// <summary>Returns true when a normalized handle matches <c>[a-z0-9_]{3,32}</c>.</summary>
    public static bool IsValid(string normalized) => HandlePattern().IsMatch(normalized);
}

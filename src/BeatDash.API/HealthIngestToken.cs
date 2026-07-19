using System.Security.Cryptography;
using System.Text;

namespace Shiron.BeatDash.API;

/// <summary>
/// Generation and hashing of the opaque per-user token a wearable/companion app uses to push
/// heart-rate samples. Only the SHA-256 hash is stored; the plaintext is shown once.
/// </summary>
public static class HealthIngestToken {
    /// <summary>A new URL-safe token with 256 bits of entropy.</summary>
    public static string Generate() {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Deterministic SHA-256 hash (base64) of a token, for storage and lookup.</summary>
    public static string Hash(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

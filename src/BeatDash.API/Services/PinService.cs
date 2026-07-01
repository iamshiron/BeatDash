using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace Shiron.BeatDash.API.Services;

public interface IPinService {
    string GeneratePin(Guid userId, out DateTime expires);
    bool TryConsumePin(string pin, out Guid userId);
}

public class PinService(IMemoryCache cache) : IPinService {
    private const int PinExpiryMinutes = 15;
    private const int MaxPin = 999999;

    public string GeneratePin(Guid userId, out DateTime expires) {
        if (cache.TryGetValue(userId.ToString(), out _)) {
            cache.Remove(userId.ToString());
        }

        expires = DateTime.UtcNow.AddMinutes(PinExpiryMinutes);
        var pin = RandomNumberGenerator.GetInt32(0, MaxPin).ToString("D6");

        var cacheOptions = new MemoryCacheEntryOptions {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(PinExpiryMinutes)
        };

        cache.Set(pin, userId, cacheOptions);
        cache.Set(userId.ToString(), pin, cacheOptions);

        return pin;
    }

    public bool TryConsumePin(string pin, out Guid userId) {
        if (cache.TryGetValue(pin, out userId)) {
            cache.Remove(pin);
            cache.Remove(userId.ToString());

            return true;
        }
        userId = Guid.Empty;
        return false;
    }
}

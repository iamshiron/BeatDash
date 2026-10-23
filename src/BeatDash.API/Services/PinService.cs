using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace Shiron.BeatDash.API.Services;

public interface IPinService {
    string GeneratePin(Guid userId, out DateTime expires);
    bool TryConsumePin(string pin, out Guid userId);
}

public sealed class PinService : IPinService {
    private const int PinExpiryMinutes = 15;
    private const int MaxPinExclusive = 1_000_000;
    private readonly IMemoryCache _cache;
    private readonly Func<int, int, int> _randomNumber;
    private readonly object _gate = new();

    public PinService(IMemoryCache cache) : this(cache, RandomNumberGenerator.GetInt32) { }

    internal PinService(IMemoryCache cache, Func<int, int, int> randomNumber) {
        _cache = cache;
        _randomNumber = randomNumber;
    }

    public string GeneratePin(Guid userId, out DateTime expires) {
        lock (_gate) {
            var userKey = userId.ToString();
            var pin = ReservePin();

            if (_cache.TryGetValue<string>(userKey, out var previousPin) && previousPin is not null) {
                _cache.Remove(previousPin);
            }

            expires = DateTime.UtcNow.AddMinutes(PinExpiryMinutes);
            var cacheOptions = new MemoryCacheEntryOptions {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(PinExpiryMinutes)
            };

            _cache.Set(pin, userId, cacheOptions);
            _cache.Set(userKey, pin, cacheOptions);

            return pin;
        }
    }

    public bool TryConsumePin(string pin, out Guid userId) {
        lock (_gate) {
            if (!_cache.TryGetValue(pin, out userId)) {
                userId = Guid.Empty;
                return false;
            }

            _cache.Remove(pin);
            var userKey = userId.ToString();
            if (_cache.TryGetValue<string>(userKey, out var activePin) && activePin == pin) {
                _cache.Remove(userKey);
            }
            return true;
        }
    }

    private string ReservePin() {
        for (var attempts = 0; attempts < MaxPinExclusive; attempts++) {
            var pin = _randomNumber(0, MaxPinExclusive).ToString("D6");
            if (!_cache.TryGetValue(pin, out _)) return pin;
        }

        throw new InvalidOperationException("No device PINs are available.");
    }
}

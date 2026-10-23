using Microsoft.Extensions.Caching.Memory;
using Shiron.BeatDash.API.Services;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class PinServiceTests {
    [Fact]
    public void GeneratePin_ValidUser_CreatesSixDigitConsumablePin() {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new PinService(cache);
        var userId = Guid.NewGuid();

        var pin = service.GeneratePin(userId, out var expires);

        Assert.Matches("^[0-9]{6}$", pin);
        Assert.InRange(expires, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));
        Assert.True(service.TryConsumePin(pin, out var consumedUserId));
        Assert.Equal(userId, consumedUserId);
        Assert.False(service.TryConsumePin(pin, out var missingUserId));
        Assert.Equal(Guid.Empty, missingUserId);
    }

    [Fact]
    public void GeneratePin_SameUserInvalidatesPreviousPin() {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new PinService(cache);
        var userId = Guid.NewGuid();

        var firstPin = service.GeneratePin(userId, out _);
        var secondPin = service.GeneratePin(userId, out _);

        Assert.False(service.TryConsumePin(firstPin, out _));
        Assert.True(service.TryConsumePin(secondPin, out var consumedUserId));
        Assert.Equal(userId, consumedUserId);
    }
}

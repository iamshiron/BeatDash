using System.Collections.Concurrent;
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
        var service = CreateService(cache, 7, 7, 8);
        var userId = Guid.NewGuid();

        var firstPin = service.GeneratePin(userId, out _);
        var secondPin = service.GeneratePin(userId, out _);

        Assert.Equal("000007", firstPin);
        Assert.Equal("000008", secondPin);
        Assert.False(service.TryConsumePin(firstPin, out _));
        Assert.True(service.TryConsumePin(secondPin, out var consumedUserId));
        Assert.Equal(userId, consumedUserId);
    }

    [Fact]
    public void GeneratePin_CollisionWithAnotherUser_ReservesDifferentPin() {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var existingUserId = Guid.NewGuid();
        cache.Set("000001", existingUserId);
        var service = CreateService(cache, 1, 2);
        var userId = Guid.NewGuid();

        var pin = service.GeneratePin(userId, out _);

        Assert.Equal("000002", pin);
        Assert.True(service.TryConsumePin("000001", out var consumedExistingUserId));
        Assert.Equal(existingUserId, consumedExistingUserId);
        Assert.True(service.TryConsumePin(pin, out var consumedUserId));
        Assert.Equal(userId, consumedUserId);
    }

    [Fact]
    public void TryConsumePin_ExpiredPin_ReturnsFalse() {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new PinService(cache);
        cache.Set("000001", Guid.NewGuid(), new MemoryCacheEntryOptions {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(-1)
        });

        var consumed = service.TryConsumePin("000001", out var userId);

        Assert.False(consumed);
        Assert.Equal(Guid.Empty, userId);
    }

    [Fact]
    public void TryConsumePin_ConcurrentRequests_ConsumesOnlyOnce() {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(cache, 1);
        var userId = Guid.NewGuid();
        var pin = service.GeneratePin(userId, out _);
        var consumers = new ConcurrentBag<Guid>();

        Parallel.For(0, 32, _ => {
            if (service.TryConsumePin(pin, out var consumedUserId)) {
                consumers.Add(consumedUserId);
            }
        });

        var consumer = Assert.Single(consumers);
        Assert.Equal(userId, consumer);
    }

    private static PinService CreateService(MemoryCache cache, params int[] pins) {
        var index = 0;
        return new PinService(cache, (_, _) => pins[index++]);
    }
}

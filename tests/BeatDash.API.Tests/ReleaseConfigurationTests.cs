using Microsoft.Extensions.Configuration;
using Shiron.BeatDash.API.Configuration;
using SkiaSharp;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class ReleaseConfigurationTests {
    [Fact]
    public void NativeImageLibrary_InstalledRuntime_EncodesImages() {
        using var bitmap = new SKBitmap(1, 1);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        Assert.NotEmpty(encoded.ToArray());
    }

    [Fact]
    public void SampleConfiguration_CommentsAndOverrides_BindsRequiredServerSettings() {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Data", "appsettings.example.json"))
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Registration:DisableSignup"] = "false",
                ["Storage:UseSsl"] = "true"
            }).Build();

        Assert.NotEmpty(configuration.GetConnectionString("Default")!);
        Assert.NotEmpty(configuration["Jwt:SecretKey"]!);
        Assert.NotEmpty(configuration["AdminUser:Password"]!);
        Assert.True(configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"));
        Assert.False(configuration.GetSection("Registration").Get<RegistrationOptions>()!.DisableSignup);
        var storage = configuration.GetSection("Storage").Get<StorageOptions>()!;
        Assert.Equal("localhost:9000", storage.Endpoint);
        Assert.True(storage.UseSsl);
    }
}

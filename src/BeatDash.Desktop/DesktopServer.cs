using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Shiron.BeatDash.API;

namespace Shiron.BeatDash.Desktop;

/// <summary>
/// The BeatDash server running inside the desktop process in single-user mode, on SQLite and
/// file-system storage in the user's data directory, serving the dashboard embedded in this assembly.
/// </summary>
public sealed class DesktopServer(DesktopPaths paths) : IAsyncDisposable {
    /// <summary>The Beat Saber mod's default server address, so pairing needs no configuration.</summary>
    private const string DefaultUrl = "http://127.0.0.1:1811";
    private const int DefaultUdpPort = 1811;

    private WebApplication? _app;

    /// <summary>Starts the server and returns the session the web view signs in with.</summary>
    public async Task<DesktopSession> StartAsync() {
        var sessionToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var builder = BeatDashHost.CreateBuilder([], new ManifestEmbeddedFileProvider(typeof(DesktopServer).Assembly, "wwwroot"));
        builder.Configuration
            .AddInMemoryCollection(Defaults())
            .AddJsonFile(paths.Settings, optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Desktop:Enabled"] = "true",
                ["Desktop:SessionToken"] = sessionToken
            });

        _app = BeatDashHost.Build(builder);
        await BeatDashHost.InitializeAsync(_app);
        await _app.StartAsync();

        // Navigate via loopback even when the server also listens on the LAN.
        var port = new Uri(_app.Urls.First()).Port;
        return new DesktopSession(new Uri($"http://127.0.0.1:{port}/api/desktop/session?token={sessionToken}"),
            _app.Lifetime.ApplicationStopping);
    }

    private Dictionary<string, string?> Defaults() => new() {
        ["Urls"] = DefaultUrl,
        // Only loopback names; a LAN binding in settings.json must widen this too.
        ["AllowedHosts"] = "127.0.0.1;localhost",
        ["Database:Provider"] = "Sqlite",
        ["ConnectionStrings:Default"] = $"Data Source={paths.Database}",
        ["Database:ApplyMigrationsOnStartup"] = "true",
        ["Storage:Provider"] = "FileSystem",
        ["Storage:RootDirectory"] = paths.Storage,
        ["Backup:WorkDirectory"] = paths.BackupWork,
        ["Jwt:SecretKey"] = GetOrCreateJwtSecret(),
        ["Jwt:Issuer"] = "BeatDash",
        ["Jwt:Audience"] = "BeatDash",
        ["UdpSocket:Host"] = "127.0.0.1",
        ["UdpSocket:Port"] = DefaultUdpPort.ToString(),
        ["Logging:LogLevel:Default"] = "Warning"
    };

    private string GetOrCreateJwtSecret() {
        if (File.Exists(paths.JwtSecret)) return File.ReadAllText(paths.JwtSecret).Trim();
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (var writer = new StreamWriter(paths.JwtSecret, options)) writer.Write(secret);
        return secret;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() {
        if (_app is null) return;
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

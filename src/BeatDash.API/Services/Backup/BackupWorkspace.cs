using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;

namespace Shiron.BeatDash.API.Services.Backup;

/// <summary>Serializes backup operations and retains recovery data across process restarts.</summary>
public sealed class BackupWorkspace(IOptions<BackupOptions> options) : IDisposable {
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal string RecoveryPath => Path.Combine(options.Value.WorkDirectory, "recovery.zip");
    internal string NewPath() {
        Directory.CreateDirectory(options.Value.WorkDirectory);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(options.Value.WorkDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return Path.Combine(options.Value.WorkDirectory, $"{Guid.NewGuid():N}.zip");
    }

    /// <inheritdoc/>
    public void Dispose() => Gate.Dispose();
}

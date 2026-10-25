namespace Shiron.BeatDash.Desktop;

/// <summary>The per-user directory holding everything the desktop app persists.</summary>
/// <param name="root">The data directory; created with owner-only permissions where supported.</param>
public sealed class DesktopPaths(string root) {
    /// <summary>Overrides the data directory, e.g. for a portable installation.</summary>
    public const string DataDirectoryVariable = "BEATDASH_DESKTOP_DATA_DIRECTORY";

    /// <summary>The absolute data directory.</summary>
    public string Root { get; } = Path.GetFullPath(root);

    /// <summary>The SQLite database file.</summary>
    public string Database => Path.Combine(Root, "beatdash.db");

    /// <summary>The file-system object storage root.</summary>
    public string Storage => Path.Combine(Root, "storage");

    /// <summary>The server's private backup workspace.</summary>
    public string BackupWork => Path.Combine(Root, "backup-work");

    /// <summary>The web view's profile: cookies, local storage and cache.</summary>
    public string WebView => Path.Combine(Root, "webview");

    /// <summary>Optional server configuration overrides, in the server's <c>appsettings.json</c> format.</summary>
    public string Settings => Path.Combine(Root, "settings.json");

    /// <summary>The persisted secret signing the Beat Saber mod's tokens, so pairings survive restarts.</summary>
    public string JwtSecret => Path.Combine(Root, "jwt-secret");

    private string InstanceLock => Path.Combine(Root, "instance.lock");

    /// <summary>The default per-user location, or <see cref="DataDirectoryVariable"/> when set.</summary>
    public static DesktopPaths Resolve() =>
        new(Environment.GetEnvironmentVariable(DataDirectoryVariable) is { Length: > 0 } directory
            ? directory
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeatDash"));

    /// <summary>Creates the data directory, readable only by the current user on Unix.</summary>
    public void Create() {
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(Root);
        else Directory.CreateDirectory(Root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>Claims the data directory for this process, or returns null when another instance already runs.</summary>
    public FileStream? TryLock() {
        try {
            return new FileStream(InstanceLock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        } catch (IOException) {
            return null;
        }
    }
}

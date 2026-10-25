using System.Runtime.InteropServices;
using Avalonia;

namespace Shiron.BeatDash.Desktop;

public static class Program {
    [STAThread]
    public static int Main(string[] args) {
        // WebKitGTK's DMA-BUF renderer leaves the view blank on common GPU drivers (notably NVIDIA).
        // WebKit reads the native environment, which Environment.SetEnvironmentVariable does not change on Unix.
        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("WEBKIT_DISABLE_DMABUF_RENDERER") is null)
            SetNativeEnvironmentVariable("WEBKIT_DISABLE_DMABUF_RENDERER", "1", 1);

        var paths = DesktopPaths.Resolve();
        paths.Create();
        using var instance = paths.TryLock();
        var server = new DesktopServer(paths);
        var session = instance is null
            ? Task.FromException<DesktopSession>(new InvalidOperationException("BeatDash is already running."))
            : Task.Run(server.StartAsync);
        try {
            return AppBuilder.Configure(() => new App(session, paths))
                .UsePlatformDetect()
                .StartWithClassicDesktopLifetime(args);
        } finally {
            // Let a start that is still in progress finish before shutting the server down.
            Task.WhenAny(session).Wait();
            // Off the UI thread: its synchronization context no longer pumps, so awaits there would never resume.
            Task.Run(() => server.DisposeAsync().AsTask()).GetAwaiter().GetResult();
        }
    }

    [DllImport("libc", EntryPoint = "setenv")]
    private static extern int SetNativeEnvironmentVariable(string name, string value, int overwrite);
}

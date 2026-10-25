using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Shiron.BeatDash.Desktop;

/// <summary>Hosts the dashboard in a native web view once the embedded server is ready.</summary>
public sealed class MainWindow : Window {
    private readonly DesktopPaths _paths;

    public MainWindow(Task<DesktopSession> session, DesktopPaths paths) {
        _paths = paths;
        Title = "BeatDash";
        Width = 1400;
        Height = 900;
        MinWidth = 800;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = Message("Starting BeatDash…");
        _ = ShowDashboardAsync(session);
    }

    private async Task ShowDashboardAsync(Task<DesktopSession> starting) {
        DesktopSession session;
        try {
            session = await starting;
        } catch (Exception ex) {
            Content = Message($"BeatDash could not start.\n\n{ex.Message}");
            return;
        }
        session.Stopping.Register(() => Dispatcher.UIThread.Post(Close));
        Content = CreateWebView(session.Address);
    }

    private NativeWebView CreateWebView(Uri session) {
        var web = new NativeWebView { Source = session };
        web.EnvironmentRequested += (_, e) => ConfigureEnvironment(e);
        web.NavigationStarted += (_, e) => {
            if (e.Request is not { } request || IsDashboard(request, session) || !IsWeb(request)) return;
            e.Cancel = true;
            OpenInBrowser(request);
        };
        web.NewWindowRequested += (_, e) => {
            e.Handled = true;
            if (e.Request is not { } request) return;
            if (IsDashboard(request, session)) web.Navigate(request);
            else if (IsWeb(request)) OpenInBrowser(request);
        };
        return web;
    }

    // Keep the web view's profile in the data directory rather than beside the executable.
    private void ConfigureEnvironment(WebViewEnvironmentRequestedEventArgs e) {
#if DEBUG
        e.EnableDevTools = true;
#endif
        switch (e) {
            case WindowsWebView2EnvironmentRequestedEventArgs webView2:
                webView2.UserDataFolder = _paths.WebView;
                break;
            case GtkWebViewEnvironmentRequestedEventArgs gtk:
                gtk.BaseDataDirectory = Path.Combine(_paths.WebView, "data");
                gtk.BaseCacheDirectory = Path.Combine(_paths.WebView, "cache");
                break;
            case LinuxWpeWebViewEnvironmentRequestedEventArgs wpe:
                wpe.DataDirectory = Path.Combine(_paths.WebView, "data");
                wpe.CacheDirectory = Path.Combine(_paths.WebView, "cache");
                break;
        }
    }

    private static bool IsDashboard(Uri request, Uri session) =>
        Uri.Compare(request, session, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;

    private static bool IsWeb(Uri request) => request.Scheme == Uri.UriSchemeHttp || request.Scheme == Uri.UriSchemeHttps;

    /// <summary>External links (BeatSaver, documentation) open in the user's browser, not inside the app.</summary>
    private static void OpenInBrowser(Uri request) =>
        Process.Start(new ProcessStartInfo(request.AbsoluteUri) { UseShellExecute = true })?.Dispose();

    private static TextBlock Message(string text) => new() {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        MaxWidth = 640
    };
}

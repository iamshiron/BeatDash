using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace Shiron.BeatDash.Desktop;

/// <summary>The desktop application: a single window around the dashboard.</summary>
public sealed class App(Task<DesktopSession> session, DesktopPaths paths) : Application {
    /// <inheritdoc/>
    public override void Initialize() => Styles.Add(new FluentTheme());

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow(session, paths);
        base.OnFrameworkInitializationCompleted();
    }
}

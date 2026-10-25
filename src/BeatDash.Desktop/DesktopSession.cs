namespace Shiron.BeatDash.Desktop;

/// <summary>A running embedded server, as the window needs it.</summary>
/// <param name="Address">The one-time sign-in address for the web view.</param>
/// <param name="Stopping">Signals when the server shuts down, e.g. on SIGTERM, so the window closes with it.</param>
public sealed record DesktopSession(Uri Address, CancellationToken Stopping);

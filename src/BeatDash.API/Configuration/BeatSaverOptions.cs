namespace Shiron.BeatDash.API.Configuration;

/// <summary>
/// Controls the BeatSaver fetch pipeline (see the Quartz jobs and
/// <c>IBeatSaverFetchService</c>). Bound from the "BeatSaver" configuration section.
/// </summary>
public sealed class BeatSaverOptions {
    /// <summary>Base URL of the BeatSaver API.</summary>
    public string ApiBaseUrl { get; set; } = "https://api.beatsaver.com";

    /// <summary>User-Agent header sent with every outbound BeatSaver request.</summary>
    public string UserAgent { get; set; } = "BeatDash/1.0 (+https://github.com/Shiron)";

    /// <summary>Run a full fetch sweep once when the application boots.</summary>
    public bool FetchOnStartup { get; set; } = true;

    /// <summary>Enable the recurring scheduled fetch sweep.</summary>
    public bool ScheduledFetchEnabled { get; set; } = true;

    /// <summary>Minutes between scheduled fetch sweeps. Must be &gt;= 1.</summary>
    public int ScheduledFetchIntervalMinutes { get; set; } = 60;

    /// <summary>Kick off a fetch as soon as a new beatmap is persisted.</summary>
    public bool FetchOnNewMap { get; set; } = true;

    /// <summary>
    /// Maximum outbound requests to BeatSaver per minute. Enforced globally across
    /// the scheduled sweep and on-new-map triggers. Must be &gt;= 1.
    /// </summary>
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>Maximum attempts before a failed map stops being retried by the sweep.</summary>
    public int MaxFetchAttempts { get; set; } = 3;

    /// <summary>Maximum maps processed per sweep. <c>0</c> means unlimited.</summary>
    public int MaxMapsPerRun { get; set; }

    /// <summary>Timeout (seconds) applied to each BeatSaver HTTP request.</summary>
    public int RequestTimeoutSeconds { get; set; } = 60;
}

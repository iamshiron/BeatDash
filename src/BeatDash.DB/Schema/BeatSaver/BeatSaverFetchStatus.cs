namespace Shiron.BeatDash.DB.Schema.BeatSaver;

/// <summary>
/// Tracks the lifecycle of a <see cref="Beatmap"/>'s BeatSaver metadata fetch.
/// </summary>
public enum BeatSaverFetchStatus {
    /// <summary>Not yet fetched; awaiting the next fetch job.</summary>
    Pending = 0,

    /// <summary>Skipped — the level id is not a downloadable custom BeatSaver map.</summary>
    Skipped = 1,

    /// <summary>Successfully fetched and persisted.</summary>
    Fetched = 2,

    /// <summary>The BeatSaver API returned no map for this hash.</summary>
    NotFound = 3,

    /// <summary>Fetch failed (network/parse/storage); eligible for retry.</summary>
    Failed = 4,
}

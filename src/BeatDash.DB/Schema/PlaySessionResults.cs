namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// Final performance results for a completed play session, persisted as an
/// owned entity on <see cref="PlaySession"/> (columns on the same table).
/// Populated from the game client's <c>LevelCompletionResults</c> on terminal
/// states. Null for in-flight or old sessions without results.
/// </summary>
public sealed class PlaySessionResults {
    /// <summary>Final score after modifier adjustments (modified score).</summary>
    public required int Score { get; set; }

    /// <summary>Raw multiplied score before modifier adjustments.</summary>
    public required int MultipliedScore { get; set; }

    /// <summary>Maximum possible multiplied score for this beatmap.</summary>
    public required int MaxPossibleScore { get; set; }

    /// <summary>Accuracy ratio (0–1): <see cref="MultipliedScore"/> / <see cref="MaxPossibleScore"/>.</summary>
    public required float Accuracy { get; set; }

    /// <summary>Letter rank (e.g. "S", "SS", "SSS").</summary>
    public required string Rank { get; set; }

    /// <summary>Whether the player never dropped combo.</summary>
    public required bool FullCombo { get; set; }

    /// <summary>Highest combo achieved during the play.</summary>
    public required int MaxCombo { get; set; }

    /// <summary>Number of good cuts.</summary>
    public required int GoodCuts { get; set; }

    /// <summary>Number of bad cuts.</summary>
    public required int BadCuts { get; set; }

    /// <summary>Number of missed notes.</summary>
    public required int Misses { get; set; }

    /// <summary>Saber energy at the end (0–1).</summary>
    public required float FinalEnergy { get; set; }

    /// <summary>
    /// Song time (ms) at which the session ended. Compared against the
    /// beatmap duration, this distinguishes a full clear from an early Quit.
    /// </summary>
    public required int EndSongTimeMs { get; set; }
}

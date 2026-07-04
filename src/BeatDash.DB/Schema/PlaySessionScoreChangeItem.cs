namespace Shiron.BeatDash.DB.Schema;

public class PlaySessionScoreChangeItem : PlaySessionItem {
    /// <summary>
    /// Absolute cumulative <c>modifiedScore</c> at this point in time — the new
    /// cumulative score (not a pre-event value).
    /// </summary>
    public required int Score { get; set; }
}

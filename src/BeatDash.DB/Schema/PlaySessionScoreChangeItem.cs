namespace Shiron.BeatDash.DB.Schema;

public class PlaySessionScoreChangeItem : PlaySessionItem {
    public required int ScoreBefore { get; set; }
}

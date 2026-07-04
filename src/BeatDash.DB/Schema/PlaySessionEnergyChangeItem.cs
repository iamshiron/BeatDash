namespace Shiron.BeatDash.DB.Schema;

public class PlaySessionEnergyChangeItem : PlaySessionItem {
    /// <summary>
    /// Energy reported by <c>gameEnergyDidChangeEvent</c> at this point in time
    /// — the new/current energy (not a pre-event value).
    /// </summary>
    public required float Energy { get; set; }
}

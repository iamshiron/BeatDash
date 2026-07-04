namespace Shiron.BeatDash.DB.Schema;

public class PlaySessionNoteItem : PlaySessionItem {
    public required ColorType ColorType { get; set; }
    public required NoteType NoteType { get; set; }
    public required CutDirection CutDirection { get; set; }
    public required int LineIndex { get; set; }
    public required int NoteLineLayer { get; set; }
    public required int Result { get; set; }
    public required int MaxScore { get; set; }
    public required float PreCutSwing { get; set; }
    public required float PostCutSwing { get; set; }
    public required float CutPointDistance { get; set; }
    public required float SaberSpeed { get; set; }
}

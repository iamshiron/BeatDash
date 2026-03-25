namespace Shiron.BeatDash.API.Models;

public class MapData {
    public bool LevelPaused { get; set; }
    public bool LevelFinished { get; set; }
    public bool LevelFailed { get; set; }
    public bool LevelQuit { get; set; }

    public string? Hash { get; set; }
    public string SongName { get; set; } = "";
    public string SongSubName { get; set; } = "";
    public string SongAuthor { get; set; } = "";
    public string Mapper { get; set; } = "";
    public string? BSRKey { get; set; }
    public string? CoverImage { get; set; }
    public int Duration { get; set; }

    public string MapType { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string? CustomDifficultyLabel { get; set; }
    public int BPM { get; set; }
    public double NJS { get; set; }
    public Modifiers Modifiers { get; set; } = new();
    public float ModifiersMultiplier { get; set; } = 1.0f;
    public bool PracticeMode { get; set; }
    public PracticeModeModifiers PracticeModeModifiers { get; set; } = new();
    public double PP { get; set; }
    public double Star { get; set; }

    public string GameVersion { get; set; } = "";
    public string PluginVersion { get; set; } = "";
    public bool IsMultiplayer { get; set; }
    public int PreviousRecord { get; set; }
    public string? PreviousBSR { get; set; }
}

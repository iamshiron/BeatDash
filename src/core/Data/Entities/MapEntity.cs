namespace Shiron.BeatDash.API.Data.Entities;

public class MapEntity {
    public long Id { get; set; }

    public string Hash { get; set; } = "";
    public string SongName { get; set; } = "";
    public string SongSubName { get; set; } = "";
    public string SongAuthor { get; set; } = "";
    public string Mapper { get; set; } = "";
    public string? BSRKey { get; set; }
    public int Duration { get; set; }
    public int BPM { get; set; }
    public string? CoverImage { get; set; }
    public string GameVersion { get; set; } = "";

    public ICollection<DifficultyEntity> Difficulties { get; set; } = [];
    public ICollection<PlaySessionEntity> PlaySessions { get; set; } = [];
}

namespace Shiron.BeatDash.API.Data.Entities;

public class DifficultyEntity {
    public long Id { get; set; }

    public long MapId { get; set; }
    public MapEntity Map { get; set; } = null!;

    public string MapType { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string? CustomDifficultyLabel { get; set; }
    public double NJS { get; set; }
    public double PP { get; set; }
    public double Star { get; set; }

    public ICollection<PlaySessionEntity> PlaySessions { get; set; } = [];
}

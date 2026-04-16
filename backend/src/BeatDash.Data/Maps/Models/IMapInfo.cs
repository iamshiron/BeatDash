namespace Shiron.BeatDash.Data.Maps.Models;

public interface IMapInfo {
    string SongName { get; }
    string SongSubName { get; }
    string SongAuthorName { get; }
    string LevelAuthorName { get; }
    int BeatsPerMinute { get; }
    string SongFileName { get; }
    string CoverImageFileName { get; }

    IList<IMapDifficultyInfo> Difficulties { get; }
}

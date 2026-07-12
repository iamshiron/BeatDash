namespace Shiron.BeatDash.API.Endpoints;

/// <summary>
/// A suggested map+difficulty to practice, chosen to target the player's weak
/// characteristics within an attainable difficulty band.
/// </summary>
public sealed record PracticeRecommendationDto(
    Guid BeatmapId,
    Guid BeatmapDifficultyId,
    string SongName,
    string SongAuthor,
    string Mapper,
    string DifficultyRank,
    string DifficultyName,
    string CharacteristicSerializedName,
    double DifficultyRating,
    double MatchScore,
    IList<string> TargetedCharacteristics
);

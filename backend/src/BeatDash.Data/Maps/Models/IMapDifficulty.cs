namespace Shiron.BeatDash.Data.Maps.Models;

public interface IMapDifficulty {
    decimal Time { get; }
    decimal LineIndex { get; }
    decimal LineLayer { get; }
    decimal Type { get; }
    decimal CutDirection { get; }
}

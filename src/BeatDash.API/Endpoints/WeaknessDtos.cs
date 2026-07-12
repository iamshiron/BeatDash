namespace Shiron.BeatDash.API.Endpoints;

/// <summary>
/// Career-wide weakness profile: cut-direction and grid marginals of the user's
/// note aggregate, plus a per-characteristic weak-spot summary.
/// </summary>
public sealed record WeaknessProfileDto(
    IList<CutDirectionCellDto> CutDirectionMatrix,
    IList<GridCellDto> GridHeatmap,
    IList<CharacteristicWeakSpotDto> WeakSpots,
    long NotesConsidered
);

/// <summary>One hand × cut-direction cell. <see cref="Hand"/> is 0 (A/left) or 1 (B/right).</summary>
public sealed record CutDirectionCellDto(
    int Hand,
    int CutDirection,
    double Accuracy,
    double MissRate,
    long Count
);

/// <summary>One hand × grid cell (<see cref="LineIndex"/> 0–3, <see cref="NoteLineLayer"/> 0–2).</summary>
public sealed record GridCellDto(
    int Hand,
    int LineIndex,
    int NoteLineLayer,
    double Accuracy,
    double MissRate,
    long Count
);

/// <summary>
/// Aggregate performance for one game-mode characteristic, with the single weakest
/// cell (lowest cut accuracy among sufficiently-sampled keys). Weakest indices are
/// <c>-1</c> when no key has enough samples.
/// </summary>
public sealed record CharacteristicWeakSpotDto(
    string Characteristic,
    double Accuracy,
    double MissRate,
    int WeakestCutDirection,
    int WeakestLineIndex,
    int WeakestNoteLineLayer
);

namespace Shiron.BeatDash.Beatmaps;

// Unified, transient beatmap model. Every Beat Saber map format (v1–v4) is
// normalised into these objects. All times are in *beats*
// (seconds = beat / bpm * 60). Grid convention shared by every format version:
//   x / lineIndex : 0..3   column, 0 = far left,  3 = far right
//   y / lineLayer : 0..2   row,    0 = bottom,     2 = top
//   color         : 0 = left saber (red), 1 = right saber (blue)
//   direction     : 0=up 1=down 2=left 3=right 4=up-left 5=up-right
//                   6=down-left 7=down-right 8=any (dot)

/// <summary>A cuttable note.</summary>
public sealed record Note {
    public required double Beat { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }

    /// <summary>0 = red/left, 1 = blue/right.</summary>
    public required int Color { get; init; }

    /// <summary>Cut direction 0..8 (8 = dot/any).</summary>
    public required int Direction { get; init; }

    /// <summary>Extra CCW rotation in degrees (v3+); 0 otherwise.</summary>
    public double AngleOffset { get; init; }
}

/// <summary>A bomb (avoid).</summary>
public sealed record Bomb {
    public required double Beat { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
}

/// <summary>A wall/obstacle.</summary>
public sealed record Obstacle {
    public required double Beat { get; init; }

    /// <summary>Bottom row the wall starts at (v3+); 0 for v2 full walls.</summary>
    public required int X { get; init; }
    public required int Y { get; init; }

    /// <summary>Duration in beats.</summary>
    public required double Duration { get; init; }

    /// <summary>Width in columns.</summary>
    public required int Width { get; init; }

    /// <summary>Height in rows (v3+); derived for v2 (5 = full, 3 = crouch).</summary>
    public required int Height { get; init; }
}

/// <summary>Burst slider / "chain" — a head note followed by a run of chained cubes.</summary>
public sealed record Chain {
    public required double Beat { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Color { get; init; }
    public required int Direction { get; init; }
    public required double TailBeat { get; init; }
    public required int TailX { get; init; }
    public required int TailY { get; init; }

    /// <summary>
    /// Chain segment field: the integer slice count in v3 (<c>sc</c>), or the raw
    /// (fractional) squish value in v4 (<c>s</c>). Kept as a double so both map
    /// verbatim onto the same field.
    /// </summary>
    public required double SliceCount { get; init; }
}

/// <summary>Slider / "arc" — a smooth swing link between two notes.</summary>
public sealed record Arc {
    public required double Beat { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Color { get; init; }
    public required int HeadDirection { get; init; }
    public required double TailBeat { get; init; }
    public required int TailX { get; init; }
    public required int TailY { get; init; }
    public required int TailDirection { get; init; }
}

/// <summary>A tempo change at a given beat.</summary>
public sealed record BpmChange {
    public required double Beat { get; init; }
    public required double Bpm { get; init; }
}

/// <summary>A single difficulty of a single characteristic.</summary>
public sealed record ParsedBeatmap {
    public required string Characteristic { get; init; }
    public required string Difficulty { get; init; }
    public required int? DifficultyRank { get; init; }

    /// <summary>Note jump movement speed.</summary>
    public required double? Njs { get; init; }

    /// <summary>Note jump start beat offset.</summary>
    public required double? NjsOffset { get; init; }

    /// <summary>The beatmap file's format version.</summary>
    public required string FormatVersion { get; init; }
    public required string Filename { get; init; }

    public IReadOnlyList<Note> Notes { get; init; } = [];
    public IReadOnlyList<Bomb> Bombs { get; init; } = [];
    public IReadOnlyList<Obstacle> Obstacles { get; init; } = [];
    public IReadOnlyList<Chain> Chains { get; init; } = [];
    public IReadOnlyList<Arc> Arcs { get; init; } = [];
    public IReadOnlyList<BpmChange> BpmChanges { get; init; } = [];

    /// <summary>Per-object-type counts, mirroring the reference parser's summary.</summary>
    public IReadOnlyDictionary<string, int> Counts => new Dictionary<string, int> {
        ["notes"] = Notes.Count,
        ["bombs"] = Bombs.Count,
        ["obstacles"] = Obstacles.Count,
        ["chains"] = Chains.Count,
        ["arcs"] = Arcs.Count,
        ["bpm_changes"] = BpmChanges.Count,
    };
}

/// <summary>A whole custom level: metadata + all its difficulties.</summary>
public sealed record ParsedLevel {
    public required string Folder { get; init; }
    public required string SongName { get; init; }
    public required string SongSubName { get; init; }
    public required string SongAuthor { get; init; }
    public required string Mapper { get; init; }
    public required string? SongFilename { get; init; }
    public required double Bpm { get; init; }
    public required string InfoVersion { get; init; }
    public IReadOnlyList<ParsedBeatmap> Beatmaps { get; init; } = [];
}

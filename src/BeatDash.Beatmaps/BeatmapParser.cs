using System.Text.Json;

namespace Shiron.BeatDash.Beatmaps;

/// <summary>Raised when a file's format/version is not recognised.</summary>
public sealed class UnknownFormatException(string message) : Exception(message);

/// <summary>
/// Parses Beat Saber custom levels of any historical on-disk format (v1–v4) into
/// the unified <see cref="ParsedLevel"/> model: notes, bombs, obstacles, chains,
/// arcs and BPM/timing. Lighting and environment events are out of scope.
/// </summary>
public static class BeatmapParser {
    // A shared empty object, used as the "not found" result when resolving v4
    // data-table indices (mirrors the Python `{}` fallback). The backing document
    // is intentionally kept alive for the process lifetime.
    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement;

    /// <summary>Parses an entire custom level into a unified <see cref="ParsedLevel"/>.</summary>
    public static ParsedLevel ParseLevel(IBeatmapFileSource source) {
        var infoBytes = source.TryReadFile("Info.dat")
            ?? throw new UnknownFormatException($"No Info.dat in '{source.Name}'");

        using var infoDoc = ParseJson(infoBytes);
        var (meta, diffs) = ParseInfo(infoDoc.RootElement);

        var beatmaps = new List<ParsedBeatmap>();
        foreach (var dm in diffs) {
            if (string.IsNullOrEmpty(dm.Filename)) continue;

            var bmBytes = source.TryReadFile(dm.Filename);
            if (bmBytes is null) continue; // Info references a file not present — skip, don't crash.

            using var bmDoc = ParseJson(bmBytes);
            if (bmDoc.RootElement.ValueKind != JsonValueKind.Object) {
                throw new UnknownFormatException(
                    $"Beatmap {dm.Filename} in '{source.Name}' is not a JSON object");
            }

            beatmaps.Add(ParseBeatmap(bmDoc.RootElement, dm));
        }

        return new ParsedLevel {
            Folder = source.Name,
            SongName = meta.SongName,
            SongSubName = meta.SongSubName,
            SongAuthor = meta.SongAuthor,
            Mapper = meta.Mapper,
            SongFilename = meta.SongFilename,
            Bpm = meta.Bpm,
            InfoVersion = meta.InfoVersion,
            Beatmaps = beatmaps,
        };
    }

    // ---------------------------------------------------------------------- //
    // Info parsing
    // ---------------------------------------------------------------------- //
    private readonly record struct LevelMeta(
        string SongName, string SongSubName, string SongAuthor, string Mapper,
        string? SongFilename, double Bpm, string InfoVersion);

    private readonly record struct DiffMeta(
        string Characteristic, string Difficulty, int? Rank,
        double? Njs, double? NjsOffset, string Filename);

    private static (LevelMeta, List<DiffMeta>) ParseInfo(JsonElement info) {
        var version = GetString(info, "_version") ?? GetString(info, "version");
        var m = Major(version);

        if (m == 4) return ParseInfoV4(info);
        if (m == 2 || info.TryGetProperty("_difficultyBeatmapSets", out _)) return ParseInfoV2(info);

        throw new UnknownFormatException($"Unrecognised Info format version={Repr(version)}");
    }

    private static (LevelMeta, List<DiffMeta>) ParseInfoV2(JsonElement info) {
        var meta = new LevelMeta(
            SongName: GetString(info, "_songName") ?? "",
            SongSubName: GetString(info, "_songSubName") ?? "",
            SongAuthor: GetString(info, "_songAuthorName") ?? "",
            Mapper: GetString(info, "_levelAuthorName") ?? "",
            SongFilename: GetString(info, "_songFilename"),
            Bpm: GetDouble(info, "_beatsPerMinute", 0.0),
            InfoVersion: GetString(info, "_version") ?? "2.0.0");

        var diffs = new List<DiffMeta>();
        foreach (var s in GetArray(info, "_difficultyBeatmapSets")) {
            var characteristic = GetString(s, "_beatmapCharacteristicName") ?? "Standard";
            foreach (var d in GetArray(s, "_difficultyBeatmaps")) {
                diffs.Add(new DiffMeta(
                    characteristic,
                    GetString(d, "_difficulty") ?? "Unknown",
                    GetIntOrNull(d, "_difficultyRank"),
                    GetDoubleOrNull(d, "_noteJumpMovementSpeed"),
                    GetDoubleOrNull(d, "_noteJumpStartBeatOffset"),
                    GetString(d, "_beatmapFilename") ?? ""));
            }
        }
        return (meta, diffs);
    }

    private static (LevelMeta, List<DiffMeta>) ParseInfoV4(JsonElement info) {
        var song = GetObject(info, "song");
        var audio = GetObject(info, "audio");

        var diffs = new List<DiffMeta>();
        var mappersSeen = new List<string>();
        foreach (var d in GetArray(info, "difficultyBeatmaps")) {
            var authors = GetObject(d, "beatmapAuthors");
            foreach (var mapper in GetArray(authors, "mappers")) {
                if (mapper.ValueKind == JsonValueKind.String) {
                    var name = mapper.GetString()!;
                    if (!mappersSeen.Contains(name)) mappersSeen.Add(name);
                }
            }
            diffs.Add(new DiffMeta(
                GetString(d, "characteristic") ?? "Standard",
                GetString(d, "difficulty") ?? "Unknown",
                Rank: null, // per-difficulty rank is absent in v4
                GetDoubleOrNull(d, "noteJumpMovementSpeed"),
                GetDoubleOrNull(d, "noteJumpStartBeatOffset"),
                GetString(d, "beatmapDataFilename") ?? ""));
        }

        var meta = new LevelMeta(
            SongName: GetString(song, "title") ?? "",
            SongSubName: GetString(song, "subTitle") ?? "",
            SongAuthor: GetString(song, "author") ?? "",
            Mapper: string.Join(", ", mappersSeen),
            SongFilename: GetString(audio, "songFilename"),
            Bpm: GetDouble(audio, "bpm", 0.0),
            InfoVersion: GetString(info, "version") ?? "4.0.0");

        return (meta, diffs);
    }

    // ---------------------------------------------------------------------- //
    // Beatmap parsing per format family
    // ---------------------------------------------------------------------- //
    private static ParsedBeatmap ParseBeatmap(JsonElement data, DiffMeta meta) {
        var version = GetString(data, "_version") ?? GetString(data, "version");
        var m = Major(version);

        if (m == 4) return ParseBeatmapV4(data, meta);
        if (m == 3) return ParseBeatmapV3(data, meta);
        if (m == 2) return ParseBeatmapV2(data, meta);
        if (m is null) {
            // No version field: the pre-v2 editor format (has "_notes") or a
            // v3-ish file that omitted the field. Sniff the keys.
            if (data.TryGetProperty("_notes", out _) || data.TryGetProperty("_obstacles", out _))
                return ParseBeatmapV2(data, meta);
            if (data.TryGetProperty("colorNotes", out _) || data.TryGetProperty("bombNotes", out _))
                return ParseBeatmapV3(data, meta);
        }

        throw new UnknownFormatException(
            $"Unrecognised beatmap format version={Repr(version)} file={meta.Filename}");
    }

    private static ParsedBeatmap ParseBeatmapV2(JsonElement data, DiffMeta meta) {
        var notes = new List<Note>();
        var bombs = new List<Bomb>();
        // v1/v2 store notes and bombs together in `_notes`; _type 3 == bomb.
        foreach (var n in GetArray(data, "_notes")) {
            var t = GetIntOrNull(n, "_type");
            var beat = GetDouble(n, "_time", 0.0);
            var x = GetInt(n, "_lineIndex", 0);
            var y = GetInt(n, "_lineLayer", 0);
            if (t == 3) {
                bombs.Add(new Bomb { Beat = beat, X = x, Y = y });
            } else if (t is 0 or 1) {
                notes.Add(new Note {
                    Beat = beat, X = x, Y = y, Color = t.Value,
                    Direction = GetInt(n, "_cutDirection", 8),
                });
            }
            // Other _type values (legacy mine variants) are ignored.
        }

        var obstacles = new List<Obstacle>();
        foreach (var o in GetArray(data, "_obstacles")) {
            var (y, h) = V2ObstacleGeometry(o);
            obstacles.Add(new Obstacle {
                Beat = GetDouble(o, "_time", 0.0),
                X = GetInt(o, "_lineIndex", 0),
                Y = y,
                Duration = GetDouble(o, "_duration", 0.0),
                Width = GetInt(o, "_width", 1),
                Height = h,
            });
        }

        return Finish(data, meta, notes, bombs, obstacles, [], [], V2BpmChanges(data));
    }

    private static (int Y, int Height) V2ObstacleGeometry(JsonElement o) {
        // v2 walls only know a coarse type: 0 = full-height, 1 = crouch/hi wall.
        // Unknown types are treated as full-height so a wall is never dropped.
        var t = GetInt(o, "_type", 0);
        return t == 1 ? (2, 3) : (0, 5);
    }

    private static List<BpmChange> V2BpmChanges(JsonElement data) {
        var outList = new List<BpmChange>();

        // v2 BPM changes live in _customData._BPMChanges (or the legacy _bpmChanges).
        if (data.TryGetProperty("_customData", out var cd) && cd.ValueKind == JsonValueKind.Object) {
            foreach (var c in FirstNonEmptyArray(cd, "_BPMChanges", "_bpmChanges")) {
                var bpm = GetDoubleOrNull(c, "_BPM") ?? GetDoubleOrNull(c, "_bpm");
                if (bpm is not null) {
                    outList.Add(new BpmChange { Beat = GetDouble(c, "_time", 0.0), Bpm = bpm.Value });
                }
            }
        }

        // Legacy: BPM changes encoded as lighting events of _type 100.
        foreach (var ev in GetArray(data, "_events")) {
            if (GetIntOrNull(ev, "_type") == 100 && ev.TryGetProperty("_value", out _)) {
                outList.Add(new BpmChange { Beat = GetDouble(ev, "_time", 0.0), Bpm = GetDouble(ev, "_value", 0.0) });
            }
        }

        return outList;
    }

    private static ParsedBeatmap ParseBeatmapV3(JsonElement data, DiffMeta meta) {
        var notes = GetArray(data, "colorNotes").Select(n => new Note {
            Beat = GetDouble(n, "b", 0.0), X = GetInt(n, "x", 0), Y = GetInt(n, "y", 0),
            Color = GetInt(n, "c", 0), Direction = GetInt(n, "d", 8), AngleOffset = GetDouble(n, "a", 0.0),
        }).ToList();

        var bombs = GetArray(data, "bombNotes").Select(b => new Bomb {
            Beat = GetDouble(b, "b", 0.0), X = GetInt(b, "x", 0), Y = GetInt(b, "y", 0),
        }).ToList();

        var obstacles = GetArray(data, "obstacles").Select(o => new Obstacle {
            Beat = GetDouble(o, "b", 0.0), X = GetInt(o, "x", 0), Y = GetInt(o, "y", 0),
            Duration = GetDouble(o, "d", 0.0), Width = GetInt(o, "w", 1), Height = GetInt(o, "h", 1),
        }).ToList();

        // sliders == arcs, burstSliders == chains
        var arcs = GetArray(data, "sliders").Select(s => new Arc {
            Beat = GetDouble(s, "b", 0.0), X = GetInt(s, "x", 0), Y = GetInt(s, "y", 0),
            Color = GetInt(s, "c", 0), HeadDirection = GetInt(s, "d", 8),
            TailBeat = GetDouble(s, "tb", 0.0), TailX = GetInt(s, "tx", 0), TailY = GetInt(s, "ty", 0),
            TailDirection = GetInt(s, "tc", 8),
        }).ToList();

        var chains = GetArray(data, "burstSliders").Select(s => new Chain {
            Beat = GetDouble(s, "b", 0.0), X = GetInt(s, "x", 0), Y = GetInt(s, "y", 0),
            Color = GetInt(s, "c", 0), Direction = GetInt(s, "d", 8),
            TailBeat = GetDouble(s, "tb", 0.0), TailX = GetInt(s, "tx", 0), TailY = GetInt(s, "ty", 0),
            SliceCount = GetDouble(s, "sc", 0.0),
        }).ToList();

        var bpm = GetArray(data, "bpmEvents").Select(e => new BpmChange {
            Beat = GetDouble(e, "b", 0.0), Bpm = GetDouble(e, "m", 0.0),
        }).ToList();

        return Finish(data, meta, notes, bombs, obstacles, chains, arcs, bpm);
    }

    private static ParsedBeatmap ParseBeatmapV4(JsonElement data, DiffMeta meta) {
        // v4 splits objects into a lightweight placement list + a de-duplicated
        // data list; the placement entry carries the beat and an index `i` into
        // the data list which carries the actual x/y/colour/direction.
        var notesData = GetArray(data, "colorNotesData").ToList();
        var bombsData = GetArray(data, "bombNotesData").ToList();
        var obstData = GetArray(data, "obstaclesData").ToList();
        var chainsData = GetArray(data, "chainsData").ToList();

        var notes = new List<Note>();
        foreach (var n in GetArray(data, "colorNotes")) {
            var d = DataAt(notesData, GetInt(n, "i", -1));
            notes.Add(new Note {
                Beat = GetDouble(n, "b", 0.0), X = GetInt(d, "x", 0), Y = GetInt(d, "y", 0),
                Color = GetInt(d, "c", 0), Direction = GetInt(d, "d", 8), AngleOffset = GetDouble(d, "a", 0.0),
            });
        }

        var bombs = new List<Bomb>();
        foreach (var b in GetArray(data, "bombNotes")) {
            var d = DataAt(bombsData, GetInt(b, "i", -1));
            bombs.Add(new Bomb { Beat = GetDouble(b, "b", 0.0), X = GetInt(d, "x", 0), Y = GetInt(d, "y", 0) });
        }

        var obstacles = new List<Obstacle>();
        foreach (var o in GetArray(data, "obstacles")) {
            var d = DataAt(obstData, GetInt(o, "i", -1));
            obstacles.Add(new Obstacle {
                Beat = GetDouble(o, "b", 0.0), X = GetInt(d, "x", 0), Y = GetInt(d, "y", 0),
                Duration = GetDouble(d, "d", 0.0), Width = GetInt(d, "w", 1), Height = GetInt(d, "h", 1),
            });
        }

        // chains: head placement `i` -> colorNotesData, tail placement `ci` -> chainsData
        var chains = new List<Chain>();
        foreach (var c in GetArray(data, "chains")) {
            var head = DataAt(notesData, GetInt(c, "i", -1));
            var tail = DataAt(chainsData, GetInt(c, "ci", -1));
            chains.Add(new Chain {
                Beat = GetDouble(c, "hb", 0.0), X = GetInt(head, "x", 0), Y = GetInt(head, "y", 0),
                Color = GetIntOrNull(head, "c") ?? GetInt(tail, "c", 0), Direction = GetInt(head, "d", 8),
                TailBeat = GetDouble(c, "tb", 0.0), TailX = GetInt(tail, "tx", 0), TailY = GetInt(tail, "ty", 0),
                SliceCount = GetDouble(tail, "s", 0.0),
            });
        }

        // arcs: head index `hi` and tail index `ti` both point into colorNotesData
        var arcs = new List<Arc>();
        foreach (var a in GetArray(data, "arcs")) {
            var head = DataAt(notesData, GetInt(a, "hi", -1));
            var tail = DataAt(notesData, GetInt(a, "ti", -1));
            arcs.Add(new Arc {
                Beat = GetDouble(a, "hb", 0.0), X = GetInt(head, "x", 0), Y = GetInt(head, "y", 0),
                Color = GetInt(head, "c", 0), HeadDirection = GetInt(head, "d", 8),
                TailBeat = GetDouble(a, "tb", 0.0), TailX = GetInt(tail, "x", 0), TailY = GetInt(tail, "y", 0),
                TailDirection = GetInt(tail, "d", 8),
            });
        }

        // v4 keeps song BPM in the Info/AudioData file, not in the beatmap.
        return Finish(data, meta, notes, bombs, obstacles, chains, arcs, []);
    }

    private static ParsedBeatmap Finish(
        JsonElement data, DiffMeta meta,
        List<Note> notes, List<Bomb> bombs, List<Obstacle> obstacles,
        List<Chain> chains, List<Arc> arcs, List<BpmChange> bpmChanges) {
        var formatVersion = FirstTruthy(GetString(data, "_version"), GetString(data, "version"))
            ?? "1.x(no-version)";
        return new ParsedBeatmap {
            Characteristic = meta.Characteristic,
            Difficulty = meta.Difficulty,
            DifficultyRank = meta.Rank,
            Njs = meta.Njs,
            NjsOffset = meta.NjsOffset,
            FormatVersion = formatVersion,
            Filename = meta.Filename,
            Notes = notes, Bombs = bombs, Obstacles = obstacles,
            Chains = chains, Arcs = arcs, BpmChanges = bpmChanges,
        };
    }

    // ---------------------------------------------------------------------- //
    // JSON helpers — mirror Python dict `.get(key, default)` semantics
    // ---------------------------------------------------------------------- //
    private static JsonDocument ParseJson(byte[] bytes) {
        // Tolerate a UTF-8 BOM, which many map editors emit.
        var start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        return JsonDocument.Parse(new ReadOnlyMemory<byte>(bytes, start, bytes.Length - start));
    }

    private static JsonElement DataAt(List<JsonElement> table, int idx) =>
        idx >= 0 && idx < table.Count ? table[idx] : EmptyObject;

    private static IEnumerable<JsonElement> GetArray(JsonElement o, string key) {
        if (o.ValueKind == JsonValueKind.Object
            && o.TryGetProperty(key, out var v)
            && v.ValueKind == JsonValueKind.Array) {
            foreach (var e in v.EnumerateArray()) yield return e;
        }
    }

    private static IEnumerable<JsonElement> FirstNonEmptyArray(JsonElement o, params string[] keys) {
        foreach (var key in keys) {
            if (o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Array && v.GetArrayLength() > 0) {
                return v.EnumerateArray();
            }
        }
        return [];
    }

    private static JsonElement GetObject(JsonElement o, string key) =>
        o.ValueKind == JsonValueKind.Object
        && o.TryGetProperty(key, out var v)
        && v.ValueKind == JsonValueKind.Object ? v : EmptyObject;

    private static string? GetString(JsonElement o, string key, string? def = null) =>
        o.ValueKind == JsonValueKind.Object
        && o.TryGetProperty(key, out var v)
        && v.ValueKind == JsonValueKind.String ? v.GetString() : def;

    private static double GetDouble(JsonElement o, string key, double def) =>
        o.ValueKind == JsonValueKind.Object
        && o.TryGetProperty(key, out var v)
        && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : def;

    private static double? GetDoubleOrNull(JsonElement o, string key) =>
        o.ValueKind == JsonValueKind.Object
        && o.TryGetProperty(key, out var v)
        && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static int GetInt(JsonElement o, string key, int def) =>
        GetIntOrNull(o, key) ?? def;

    private static int? GetIntOrNull(JsonElement o, string key) {
        if (o.ValueKind == JsonValueKind.Object
            && o.TryGetProperty(key, out var v)
            && v.ValueKind == JsonValueKind.Number) {
            return v.TryGetInt32(out var i) ? i : (int) v.GetDouble();
        }
        return null;
    }

    private static int? Major(string? version) {
        if (string.IsNullOrEmpty(version)) return null;
        var first = version.Split('.')[0];
        return int.TryParse(first, out var m) ? m : null;
    }

    private static string? FirstTruthy(params string?[] values) {
        foreach (var v in values) {
            if (!string.IsNullOrEmpty(v)) return v;
        }
        return null;
    }

    private static string Repr(string? s) => s is null ? "None" : $"'{s}'";
}

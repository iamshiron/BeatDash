using Microsoft.EntityFrameworkCore;
using Mono.TextTemplating;
using Shiron.BeatDash.API.Data.Entities;

namespace Shiron.BeatDash.API.Data;

public static class BeatDashSeeder {
    private static readonly Random Rng = new(42);

    public static void SeedMockData(this BeatDashDbContext context) {
        if (context.PlaySessions.Any()) return;

        var maps = CreateMaps();
        context.Maps.AddRange(maps);
        context.SaveChanges();

        var difficulties = CreateDifficulties(maps);
        context.Difficulties.AddRange(difficulties);
        context.SaveChanges();

        var sessions = CreatePlaySessions(maps, difficulties);
        context.PlaySessions.AddRange(sessions);
        context.SaveChanges();
    }

    private static MapEntity[] CreateMaps() {
        var maps = new[]
        {
            new MapEntity { Hash = "1a2b3c4d5e6f789012345678901234567890abcd", SongName = "Believer", SongSubName = "", SongAuthor = "Imagine Dragons", Mapper = "Ramen", BSRKey = "1a2b", Duration = 204, BPM = 125, GameVersion = "1.29.1" },
            new MapEntity { Hash = "2b3c4d5e6f789012345678901234567890abcde", SongName = "Thunder", SongSubName = "", SongAuthor = "Imagine Dragons", Mapper = "GreatYazer", BSRKey = "2b3c", Duration = 187, BPM = 168, GameVersion = "1.29.1" },
            new MapEntity { Hash = "3c4d5e6f789012345678901234567890abcdef", SongName = "Unreeeal Tournament", SongSubName = "", SongAuthor = "Kenet & Rez", Mapper = "Reaxt", BSRKey = "3c4d", Duration = 135, BPM = 160, GameVersion = "1.30.0" },
            new MapEntity { Hash = "4d5e6f789012345678901234567890abcdef0", SongName = "Undefeatable", SongSubName = "Sonic Frontiers", SongAuthor = "Tommee Profitt", Mapper = "Hower", BSRKey = "4d5e", Duration = 258, BPM = 170, GameVersion = "1.29.1" },
            new MapEntity { Hash = "5e6f789012345678901234567890abcdef01", SongName = "Cotton Eye Joe", SongSubName = "", SongAuthor = "Rednex", Mapper = "Awfulnado", BSRKey = "5e6f", Duration = 192, BPM = 132, GameVersion = "1.29.1" },
            new MapEntity { Hash = "6f789012345678901234567890abcdef0123", SongName = "Fit Beat", SongSubName = "", SongAuthor = "KeitaroGrooves", Mapper = "Oqitru", BSRKey = "6f78", Duration = 178, BPM = 140, GameVersion = "1.30.0" },
            new MapEntity { Hash = "789012345678901234567890abcdef01234567", SongName = "Ghost", SongSubName = "", SongAuthor = "Bad Computer", Mapper = "Cerret", BSRKey = "7890", Duration = 224, BPM = 150, GameVersion = "1.29.1" },
            new MapEntity { Hash = "89012345678901234567890abcdef0123456789", SongName = "Crabrave", SongSubName = "", SongAuthor = "Noisestorm", Mapper = "Skyler", BSRKey = "8901", Duration = 213, BPM = 125, GameVersion = "1.29.1" },
            new MapEntity { Hash = "9012345678901234567890abcdef01234567890", SongName = "Boulevard of Broken Dreams", SongSubName = "", SongAuthor = "Green Day", Mapper = "Stygs", BSRKey = "9012", Duration = 262, BPM = 170, GameVersion = "1.30.0" },
            new MapEntity { Hash = "012345678901234567890abcdef012345678901", SongName = "Shut Up and Dance", SongSubName = "", SongAuthor = "WALK THE MOON", Mapper = "Oscar", BSRKey = "0123", Duration = 198, BPM = 128, GameVersion = "1.29.1" },
            new MapEntity { Hash = "12345678901a234567890abcdef0123456789012", SongName = "My Songs Know What You Did", SongSubName = "", SongAuthor = "Fall Out Boy", Mapper = "Halloweenvt", BSRKey = "1234", Duration = 185, BPM = 156, GameVersion = "1.29.1" },
            new MapEntity { Hash = "23456789012b34567890abcdef01234567890123", SongName = "Angel With A Shotgun", SongSubName = "", SongAuthor = "The Cab", Mapper = "Ponethefoofa", BSRKey = "2345", Duration = 208, BPM = 136, GameVersion = "1.29.1" },
            new MapEntity { Hash = "34567890123c4567890abcdef012345678901234", SongName = "Ropes", SongSubName = "", SongAuthor = "Dirty Palm", Mapper = "Day Of Joy", BSRKey = "3456", Duration = 142, BPM = 126, GameVersion = "1.30.0" },
            new MapEntity { Hash = "45678901234d567890abcdef0123456789012345", SongName = "I Want It All", SongSubName = "", SongAuthor = "Amaranthe", Mapper = "Ianawesome", BSRKey = "4567", Duration = 196, BPM = 105, GameVersion = "1.29.1" },
            new MapEntity { Hash = "56789012345e67890abcdef01234567890123456", SongName = "Internet Yamero", SongSubName = "", SongAuthor = "t+pazolite", Mapper = "Rumpus", BSRKey = "5678", Duration = 164, BPM = 175, GameVersion = "1.29.1" },
            new MapEntity { Hash = "67890123456f7890abcdef0123456789012345678", SongName = "Sail", SongSubName = "", SongAuthor = "AWOLNATION", Mapper = "GreatYazer", BSRKey = "6789", Duration = 255, BPM = 190, GameVersion = "1.29.1" },
            new MapEntity { Hash = "78901234567a890abcdef01234567890123456789", SongName = "Free Bird", SongSubName = "", SongAuthor = "Lynyrd Skynyrd", Mapper = "Havoc21", BSRKey = "789a", Duration = 562, BPM = 144, GameVersion = "1.30.0" },
            new MapEntity { Hash = "89012345678b901abcdef012345678901234567890", SongName = "Monster", SongSubName = "", SongAuthor = "Skillet", Mapper = "Syndr0me", BSRKey = "890b", Duration = 178, BPM = 137, GameVersion = "1.29.1" },
            new MapEntity { Hash = "90123456789c012abcdef0123456789012345678901", SongName = "Burn the House Down", SongSubName = "", SongAuthor = "AJR", Mapper = "BennyDaBeast", BSRKey = "901c", Duration = 193, BPM = 120, GameVersion = "1.29.1" },
            new MapEntity { Hash = "01234567890d123abcdef01234567890123456789012", SongName = "BANGARANG", SongSubName = "", SongAuthor = "Skrillex", Mapper = "Jon", BSRKey = "012d", Duration = 234, BPM = 110, GameVersion = "1.29.1" },
            new MapEntity { Hash = "12345678901e2345abcdef012345678901234567890123", SongName = "Call Me Maybe", SongSubName = "", SongAuthor = "Carly Rae Jepsen", Mapper = "Moist", BSRKey = "123e", Duration = 195, BPM = 120, GameVersion = "1.29.1" },
            new MapEntity { Hash = "23456789012f3456abcdef0123456789012345678901234", SongName = "Miserable", SongSubName = "", SongAuthor = "Dodie", Mapper = "Hexafluorine", BSRKey = "234f", Duration = 167, BPM = 80, GameVersion = "1.30.0" },
            new MapEntity { Hash = "3456789012345678abcdef01234567890123456789012345", SongName = "Breathing", SongSubName = "", SongAuthor = "Yellow Days", Mapper = "HundredDee", BSRKey = "3456", Duration = 234, BPM = 92, GameVersion = "1.29.1" },
            new MapEntity { Hash = "4567890123456789abcdef012345678901234567890123456", SongName = "Levels", SongSubName = "", SongAuthor = "Avicii", Mapper = "Sjoerd", BSRKey = "4567", Duration = 213, BPM = 126, GameVersion = "1.29.1" },
            new MapEntity { Hash = "5678901234567890abcdef0123456789012345678901234567", SongName = "The Pretender", SongSubName = "", SongAuthor = "Foo Fighters", Mapper = "GreatYazer", BSRKey = "5678", Duration = 269, BPM = 170, GameVersion = "1.29.1" },
            new MapEntity { Hash = "6789012345678901abcdef01234567890123456789012345678", SongName = "The Sound of Silence", SongSubName = "", SongAuthor = "Disturbed", Mapper = "Awfulnado", BSRKey = "6789", Duration = 265, BPM = 78, GameVersion = "1.30.0" },
            new MapEntity { Hash = "7890123456789012abcdef012345678901234567890123456789", SongName = "When I Come Around", SongSubName = "", SongAuthor = "Green Day", Mapper = "Gamer", BSRKey = "789a", Duration = 167, BPM = 104, GameVersion = "1.29.1" },
            new MapEntity { Hash = "8901234567890123abcdef0123456789012345678901234567890", SongName = "Renegade", SongSubName = "", SongAuthor = "Styx", Mapper = "Hower", BSRKey = "890b", Duration = 274, BPM = 127, GameVersion = "1.29.1" },
            new MapEntity { Hash = "9012345678901234abcdef01234567890123456789012345678901", SongName = "Lvl Insomnia", SongSubName = "", SongAuthor = "Avicii", Mapper = "Insomnia", BSRKey = "901c", Duration = 186, BPM = 126, GameVersion = "1.29.1" },
            new MapEntity { Hash = "0123456789012345abcdef012345678901234567890123456789012", SongName = "Gas Gas Gas", SongSubName = "", SongAuthor = "Manuel", Mapper = "Rasutra", BSRKey = "012d", Duration = 184, BPM = 125, GameVersion = "1.29.1" },
        };

        return maps;
    }

    private static DifficultyEntity[] CreateDifficulties(MapEntity[] maps) {
        var difficulties = new List<DifficultyEntity>();
        var diffLevels = new[] { "Easy", "Normal", "Hard", "Expert", "ExpertPlus" };
        var njsRanges = new (double Min, double Max)[] { (10, 12), (12, 14), (14, 16), (16, 18), (18, 23) };
        var ppRanges = new (double Min, double Max)[] { (0, 0), (0, 80), (80, 200), (200, 400), (350, 550) };
        var starRanges = new (double Min, double Max)[] { (0, 0), (1, 3), (3, 5), (5, 8), (7, 14) };
        var rankedChance = new[] { 0.2, 0.4, 0.6, 0.7, 0.5 };
        var seen = new HashSet<(long MapId, string MapType, string Difficulty)>();

        foreach (var map in maps) {
            var hasStandard = Rng.NextDouble() < 0.95;
            var hasOneSaber = Rng.NextDouble() < 0.3;
            var hasNoArrows = Rng.NextDouble() < 0.25;
            var has90Degree = Rng.NextDouble() < 0.2;
            var hasLawless = Rng.NextDouble() < 0.15;

            var selectedTypes = new List<(string Type, int StartDiff)>();
            if (hasStandard) selectedTypes.Add(("Standard", 0));
            if (hasOneSaber) selectedTypes.Add(("OneSaber", 0));
            if (hasNoArrows) selectedTypes.Add(("NoArrows", 0));
            if (has90Degree) selectedTypes.Add(("90Degree", 1));
            if (hasLawless) selectedTypes.Add(("Lawless", 2));

            foreach (var (mapType, startDiff) in selectedTypes) {
                var numDiffs = Rng.Next(3, 6);
                var diffOffset = Rng.Next(0, 2);
                var addedForType = new HashSet<int>();

                for (var i = 0; i < numDiffs; i++) {
                    var diffIdx = Math.Min(startDiff + diffOffset + i, diffLevels.Length - 1);
                    if (!addedForType.Add(diffIdx)) continue;

                    var key = (map.Id, mapType, diffLevels[diffIdx]);
                    if (!seen.Add(key)) continue;

                    var njsRange = njsRanges[diffIdx];
                    var ppRange = ppRanges[diffIdx];
                    var starRange = starRanges[diffIdx];
                    var isRanked = Rng.NextDouble() < rankedChance[diffIdx];

                    difficulties.Add(new DifficultyEntity {
                        MapId = map.Id,
                        Map = map,
                        MapType = mapType,
                        Difficulty = diffLevels[diffIdx],
                        NJS = Math.Round(njsRange.Min + Rng.NextDouble() * (njsRange.Max - njsRange.Min), 1),
                        PP = isRanked ? Math.Round(ppRange.Min + Rng.NextDouble() * (ppRange.Max - ppRange.Min), 2) : 0,
                        Star = isRanked ? Math.Round(starRange.Min + Rng.NextDouble() * (starRange.Max - starRange.Min), 2) : 0,
                    });
                }
            }
        }

        return difficulties.ToArray();
    }

    private static PlaySessionEntity[] CreatePlaySessions(MapEntity[] maps, DifficultyEntity[] difficulties) {
        var sessions = new List<PlaySessionEntity>();
        var endReasons = new[] { "Clear", "Quit", "Fail", "Restart" };
        var endReasonWeights = new[] { 0.60, 0.25, 0.10, 0.05 };
        var ranks = new[] { "SSS", "SS", "S", "A", "B", "C", "D", "E" };
        var pluginVersions = new[] { "2.3.3", "2.3.4", "2.4.0", "2.4.1" };
        var baseTime = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var numDays = 21;
        var sessionsPerDay = 12;

        var standardDiffs = difficulties.Where(d => d.MapType == "Standard").ToList();

        for (var day = 0; day < numDays; day++) {
            var dayStart = baseTime.AddDays(day);
            for (var s = 0; s < sessionsPerDay; s++) {
                var difficulty = standardDiffs[Rng.Next(standardDiffs.Count)];
                var map = difficulty.Map;

                var endReasonIdx = WeightedRandom(endReasonWeights);
                var endReason = endReasons[endReasonIdx];

                var isClear = endReason == "Clear";
                var isFail = endReason == "Fail";
                var isQuit = endReason == "Quit";

                double accuracy;
                int misses;
                string rank;

                if (isClear) {
                    accuracy = 85 + Rng.NextDouble() * 15;
                    misses = accuracy > 95 ? Rng.Next(0, 5) : Rng.Next(0, 30);
                    rank = GetRank(accuracy);
                } else if (isFail) {
                    accuracy = 40 + Rng.NextDouble() * 35;
                    misses = 15 + Rng.Next(0, 100);
                    rank = GetRank(accuracy);
                } else {
                    accuracy = 50 + Rng.NextDouble() * 40;
                    misses = Rng.Next(5, 50);
                    rank = GetRank(accuracy);
                }

                var maxScore = CalculateMaxScore(map.Duration);
                var score = (int) (maxScore * accuracy / 100);

                var modifiers = GenerateModifiers(isFail);
                var multiplier = CalculateMultiplier(modifiers);
                var practiceMode = Rng.NextDouble() < 0.15;

                var hour = Rng.Next(10, 24);
                var minute = Rng.Next(0, 60);
                var startedAt = dayStart.AddHours(hour).AddMinutes(minute);
                DateTimeOffset? finishedAt = isQuit ? null : startedAt.AddSeconds(map.Duration);

                var session = new PlaySessionEntity {
                    StartedAt = startedAt,
                    FinishedAt = finishedAt,
                    EndReason = isQuit ? null : endReason,
                    MapId = map.Id,
                    Map = map,
                    DifficultyId = difficulty.Id,
                    Difficulty = difficulty,
                    ModifiersMultiplier = multiplier,
                    PracticeMode = practiceMode,
                    PluginVersion = pluginVersions[Rng.Next(pluginVersions.Length)],
                    IsMultiplayer = Rng.NextDouble() < 0.1,
                    PreviousRecord = Rng.Next(0, (int) (maxScore * 0.95)),
                    PreviousBSR = Rng.NextDouble() < 0.3 ? maps[Rng.Next(maps.Length)].BSRKey : null,
                    Modifiers = modifiers,
                    PracticeModeModifiers = practiceMode ? GeneratePracticeModifiers() : new PracticeModeModifiersEntity(),
                    FinalScore = score,
                    FinalScoreWithMultipliers = (int) (score * multiplier),
                    FinalMaxScore = maxScore,
                    FinalMaxScoreWithMultipliers = (int) (maxScore * multiplier),
                    FinalRank = isQuit ? null : rank,
                    FinalFullCombo = misses == 0,
                    FinalCombo = score / 115,
                    FinalMisses = misses,
                    FinalAccuracy = Math.Round(accuracy, 2),
                    FinalTimeElapsed = (int) (map.Duration * (isQuit ? Rng.NextDouble() * 0.5 + 0.3 : 1)),
                };

                sessions.Add(session);
            }
        }

        return sessions.ToArray();
    }

    private static int WeightedRandom(double[] weights) {
        var roll = Rng.NextDouble();
        var cumulative = 0.0;
        for (var i = 0; i < weights.Length; i++) {
            cumulative += weights[i];
            if (roll < cumulative) return i;
        }
        return weights.Length - 1;
    }

    private static string GetRank(double accuracy) {
        if (accuracy >= 95) return "SSS";
        if (accuracy >= 90) return "SS";
        if (accuracy >= 85) return "S";
        if (accuracy >= 80) return "A";
        if (accuracy >= 70) return "B";
        if (accuracy >= 60) return "C";
        if (accuracy >= 50) return "D";
        return "E";
    }

    private static int CalculateMaxScore(int duration) {
        return duration * 4 * 115;
    }

    private static ModifiersEntity GenerateModifiers(bool isFail) {
        var useNoFail = isFail && Rng.NextDouble() < 0.4;
        return new ModifiersEntity {
            NoFailOn0Energy = useNoFail,
            OneLife = !useNoFail && Rng.NextDouble() < 0.05,
            FourLives = !useNoFail && Rng.NextDouble() < 0.1,
            NoBombs = Rng.NextDouble() < 0.1,
            NoWalls = Rng.NextDouble() < 0.05,
            NoArrows = Rng.NextDouble() < 0.05,
            GhostNotes = Rng.NextDouble() < 0.1,
            DisappearingArrows = Rng.NextDouble() < 0.15,
            SmallNotes = Rng.NextDouble() < 0.05,
            ProMode = Rng.NextDouble() < 0.05,
            StrictAngles = Rng.NextDouble() < 0.05,
            ZenMode = Rng.NextDouble() < 0.02,
            SlowerSong = Rng.NextDouble() < 0.1,
            FasterSong = Rng.NextDouble() < 0.15,
            SuperFastSong = Rng.NextDouble() < 0.05,
        };
    }

    private static float CalculateMultiplier(ModifiersEntity mods) {
        var mult = 1.0f;
        if (mods.NoFailOn0Energy) mult *= 0.5f;
        if (mods.OneLife) mult *= 1.0f;
        if (mods.FourLives) mult *= 0.8f;
        if (mods.NoBombs) mult *= 0.9f;
        if (mods.NoWalls) mult *= 0.9f;
        if (mods.NoArrows) mult *= 0.7f;
        if (mods.GhostNotes) mult *= 1.11f;
        if (mods.DisappearingArrows) mult *= 1.07f;
        if (mods.SmallNotes) mult *= 1.05f;
        if (mods.ProMode) mult *= 1.06f;
        if (mods.StrictAngles) mult *= 1.06f;
        if (mods.ZenMode) mult *= 0.0f;
        if (mods.SlowerSong) mult *= 0.7f;
        if (mods.FasterSong) mult *= 1.08f;
        if (mods.SuperFastSong) mult *= 1.12f;
        return mult;
    }

    private static PracticeModeModifiersEntity GeneratePracticeModifiers() {
        return new PracticeModeModifiersEntity {
            SongSpeedMul = 0.7f + (float) Rng.NextDouble() * 0.6f,
            StartInAdvanceAndClearNotes = Rng.NextDouble() < 0.3,
            SongStartTime = (float) Rng.NextDouble() * 60f,
        };
    }
}

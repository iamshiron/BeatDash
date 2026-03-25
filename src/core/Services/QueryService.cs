using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API.Data;
using Shiron.BeatDash.API.DTOs;
using Shiron.BeatDash.API.Data.Entities;

namespace Shiron.BeatDash.API.Services;

public class QueryService : IQueryService {
    private readonly BeatDashDbContext _context;

    public QueryService(BeatDashDbContext context) {
        _context = context;
    }

    public async Task<MapDto?> GetMapByIdAsync(long id) {
        var map = await _context.Maps
            .Include(m => m.Difficulties)
            .Include(m => m.PlaySessions)
            .FirstOrDefaultAsync(m => m.Id == id);

        return map == null ? null : MapToDto(map);
    }

    public async Task<MapDto?> GetMapByHashAsync(string hash) {
        var map = await _context.Maps
            .Include(m => m.Difficulties)
            .Include(m => m.PlaySessions)
            .FirstOrDefaultAsync(m => m.Hash == hash);

        return map == null ? null : MapToDto(map);
    }

    public async Task<MapDto?> GetMapByBSRKeyAsync(string bsrKey) {
        var map = await _context.Maps
            .Include(m => m.Difficulties)
            .Include(m => m.PlaySessions)
            .FirstOrDefaultAsync(m => m.BSRKey == bsrKey);

        return map == null ? null : MapToDto(map);
    }

    public async Task<PaginatedResult<MapSummaryDto>> GetMapsAsync(MapFilterParams filter) {
        var query = _context.Maps
            .Include(m => m.PlaySessions)
            .Include(m => m.Difficulties)
            .AsQueryable();

        if (!string.IsNullOrEmpty(filter.Search)) {
            var search = $"%{filter.Search}%";
            query = query.Where(m =>
                EF.Functions.Like(m.SongName, search) ||
                EF.Functions.Like(m.SongAuthor, search) ||
                EF.Functions.Like(m.Mapper, search));
        }

        if (!string.IsNullOrEmpty(filter.SongAuthor)) {
            query = query.Where(m => m.SongAuthor == filter.SongAuthor);
        }

        if (!string.IsNullOrEmpty(filter.Mapper)) {
            query = query.Where(m => m.Mapper == filter.Mapper);
        }

        if (filter.MinBPM.HasValue) {
            query = query.Where(m => m.BPM >= filter.MinBPM.Value);
        }

        if (filter.MaxBPM.HasValue) {
            query = query.Where(m => m.BPM <= filter.MaxBPM.Value);
        }

        if (filter.MinDuration.HasValue) {
            query = query.Where(m => m.Duration >= filter.MinDuration.Value);
        }

        if (filter.MaxDuration.HasValue) {
            query = query.Where(m => m.Duration <= filter.MaxDuration.Value);
        }

        if (!string.IsNullOrEmpty(filter.BSRKey)) {
            query = query.Where(m => m.BSRKey == filter.BSRKey);
        }

        if (filter.HasBeenPlayed.HasValue) {
            if (filter.HasBeenPlayed.Value) {
                query = query.Where(m => m.PlaySessions.Any());
            } else {
                query = query.Where(m => !m.PlaySessions.Any());
            }
        }

        var totalCount = await query.CountAsync();

        query = ApplySorting(query, filter.SortBy, filter.SortDescending);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(m => new MapSummaryDto {
                Id = m.Id,
                Hash = m.Hash,
                SongName = m.SongName,
                SongSubName = m.SongSubName,
                SongAuthor = m.SongAuthor,
                Mapper = m.Mapper,
                BSRKey = m.BSRKey,
                Duration = m.Duration,
                BPM = m.BPM,
                HasCoverImage = m.CoverImage != null,
                PlayCount = m.PlaySessions.Count,
                DifficultyCount = m.Difficulties.Count
            })
            .ToListAsync();

        return new PaginatedResult<MapSummaryDto> {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<List<DifficultyDto>> GetMapDifficultiesAsync(long mapId) {
        var difficulties = await _context.Difficulties
            .Include(d => d.PlaySessions)
            .Where(d => d.MapId == mapId)
            .ToListAsync();

        return difficulties.Select(d => new DifficultyDto {
            Id = d.Id,
            MapId = d.MapId,
            MapType = d.MapType,
            Difficulty = d.Difficulty,
            CustomDifficultyLabel = d.CustomDifficultyLabel,
            NJS = d.NJS,
            PP = d.PP,
            Star = d.Star,
            PlayCount = d.PlaySessions.Count
        }).ToList();
    }

    public async Task<PaginatedResult<PlaySessionSummaryDto>> GetPlaySessionsAsync(PlaySessionFilterParams filter) {
        var query = _context.PlaySessions
            .Include(s => s.Map)
            .Include(s => s.Difficulty)
            .AsQueryable();

        if (filter.MapId.HasValue) {
            query = query.Where(s => s.MapId == filter.MapId.Value);
        }

        if (!string.IsNullOrEmpty(filter.MapHash)) {
            query = query.Where(s => s.Map.Hash == filter.MapHash);
        }

        if (!string.IsNullOrEmpty(filter.EndReason)) {
            query = query.Where(s => s.EndReason == filter.EndReason);
        }

        if (filter.FromDate.HasValue) {
            query = query.Where(s => s.StartedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue) {
            query = query.Where(s => s.StartedAt <= filter.ToDate.Value);
        }

        if (filter.PracticeMode.HasValue) {
            query = query.Where(s => s.PracticeMode == filter.PracticeMode.Value);
        }

        if (filter.Multiplayer.HasValue) {
            query = query.Where(s => s.IsMultiplayer == filter.Multiplayer.Value);
        }

        if (!string.IsNullOrEmpty(filter.Difficulty)) {
            query = query.Where(s => s.Difficulty.Difficulty == filter.Difficulty);
        }

        if (!string.IsNullOrEmpty(filter.MapType)) {
            query = query.Where(s => s.Difficulty.MapType == filter.MapType);
        }

        if (filter.FullCombo.HasValue) {
            query = query.Where(s => s.FinalFullCombo == filter.FullCombo.Value);
        }

        if (filter.MinAccuracy.HasValue) {
            query = query.Where(s => s.FinalAccuracy >= filter.MinAccuracy.Value);
        }

        if (filter.MaxAccuracy.HasValue) {
            query = query.Where(s => s.FinalAccuracy <= filter.MaxAccuracy.Value);
        }

        var totalCount = await query.CountAsync();

        query = ApplySessionSorting(query, filter.SortBy, filter.SortDescending);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(s => new PlaySessionSummaryDto {
                Id = s.Id,
                StartedAt = s.StartedAt,
                FinishedAt = s.FinishedAt,
                EndReason = s.EndReason,
                MapId = s.MapId,
                SongName = s.Map.SongName,
                SongAuthor = s.Map.SongAuthor,
                Mapper = s.Map.Mapper,
                HasCoverImage = s.Map.CoverImage != null,
                Difficulty = s.Difficulty.Difficulty,
                MapType = s.Difficulty.MapType,
                FinalScore = s.FinalScore,
                FinalMaxScore = s.FinalMaxScore,
                FinalRank = s.FinalRank,
                FinalAccuracy = s.FinalAccuracy,
                FinalFullCombo = s.FinalFullCombo,
                FinalMisses = s.FinalMisses
            })
            .ToListAsync();

        return new PaginatedResult<PlaySessionSummaryDto> {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<PlaySessionDto?> GetPlaySessionByIdAsync(long id) {
        var session = await _context.PlaySessions
            .Include(s => s.Map)
                .ThenInclude(m => m.Difficulties)
            .Include(s => s.Map)
                .ThenInclude(m => m.PlaySessions)
            .Include(s => s.Difficulty)
            .Include(s => s.Modifiers)
            .Include(s => s.PracticeModeModifiers)
            .FirstOrDefaultAsync(s => s.Id == id);

        return session == null ? null : SessionToDto(session);
    }

    public async Task<List<PlaySessionSummaryDto>> GetRecentSessionsAsync(int count = 10) {
        return await _context.PlaySessions
            .Include(s => s.Map)
            .Include(s => s.Difficulty)
            .OrderByDescending(s => s.Id)
            .Take(count)
            .Select(s => new PlaySessionSummaryDto {
                Id = s.Id,
                StartedAt = s.StartedAt,
                FinishedAt = s.FinishedAt,
                EndReason = s.EndReason,
                SongName = s.Map.SongName,
                SongAuthor = s.Map.SongAuthor,
                Mapper = s.Map.Mapper,
                HasCoverImage = s.Map.CoverImage != null,
                Difficulty = s.Difficulty.Difficulty,
                MapType = s.Difficulty.MapType,
                FinalScore = s.FinalScore,
                FinalMaxScore = s.FinalMaxScore,
                FinalRank = s.FinalRank,
                FinalAccuracy = s.FinalAccuracy,
                FinalFullCombo = s.FinalFullCombo,
                FinalMisses = s.FinalMisses
            })
            .ToListAsync();
    }

    public async Task<List<PlaySessionSummaryDto>> GetSessionsByMapIdAsync(long mapId, int count = 20) {
        return await _context.PlaySessions
            .Include(s => s.Map)
            .Include(s => s.Difficulty)
            .Where(s => s.MapId == mapId)
            .OrderByDescending(s => s.Id)
            .Take(count)
            .Select(s => new PlaySessionSummaryDto {
                Id = s.Id,
                StartedAt = s.StartedAt,
                FinishedAt = s.FinishedAt,
                EndReason = s.EndReason,
                SongName = s.Map.SongName,
                SongAuthor = s.Map.SongAuthor,
                Mapper = s.Map.Mapper,
                HasCoverImage = s.Map.CoverImage != null,
                Difficulty = s.Difficulty.Difficulty,
                MapType = s.Difficulty.MapType,
                FinalScore = s.FinalScore,
                FinalMaxScore = s.FinalMaxScore,
                FinalRank = s.FinalRank,
                FinalAccuracy = s.FinalAccuracy,
                FinalFullCombo = s.FinalFullCombo,
                FinalMisses = s.FinalMisses
            })
            .ToListAsync();
    }

    public async Task<PaginatedResult<LiveDataSnapshotDto>> GetLiveDataSnapshotsAsync(LiveDataSnapshotFilterParams filter) {
        var query = _context.LiveDataSnapshots.AsQueryable();

        if (filter.PlaySessionId.HasValue) {
            query = query.Where(s => s.PlaySessionId == filter.PlaySessionId.Value);
        }

        if (filter.FromTimestamp.HasValue) {
            query = query.Where(s => s.Timestamp >= filter.FromTimestamp.Value);
        }

        if (filter.ToTimestamp.HasValue) {
            query = query.Where(s => s.Timestamp <= filter.ToTimestamp.Value);
        }

        if (filter.EventTrigger.HasValue) {
            query = query.Where(s => s.EventTrigger == filter.EventTrigger.Value);
        }

        if (filter.FullCombo.HasValue) {
            query = query.Where(s => s.FullCombo == filter.FullCombo.Value);
        }

        if (filter.MinAccuracy.HasValue) {
            query = query.Where(s => s.Accuracy >= filter.MinAccuracy.Value);
        }

        if (filter.MaxAccuracy.HasValue) {
            query = query.Where(s => s.Accuracy <= filter.MaxAccuracy.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(s => s.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(s => new LiveDataSnapshotDto {
                Id = s.Id,
                Timestamp = s.Timestamp,
                PlaySessionId = s.PlaySessionId,
                Score = s.Score,
                ScoreWithMultipliers = s.ScoreWithMultipliers,
                MaxScore = s.MaxScore,
                MaxScoreWithMultipliers = s.MaxScoreWithMultipliers,
                Rank = s.Rank,
                FullCombo = s.FullCombo,
                NotesSpawned = s.NotesSpawned,
                Combo = s.Combo,
                Misses = s.Misses,
                Accuracy = s.Accuracy,
                PlayerHealth = s.PlayerHealth,
                TimeElapsed = s.TimeElapsed,
                EventTrigger = s.EventTrigger,
                BlockHitPreSwing = s.BlockHitPreSwing,
                BlockHitPostSwing = s.BlockHitPostSwing,
                BlockHitCenterSwing = s.BlockHitCenterSwing,
                NoteColorType = s.NoteColorType
            })
            .ToListAsync();

        return new PaginatedResult<LiveDataSnapshotDto> {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<List<LiveDataSnapshotDto>> GetSnapshotsBySessionIdAsync(long sessionId) {
        return await _context.LiveDataSnapshots
            .Where(s => s.PlaySessionId == sessionId)
            .OrderBy(s => s.Id)
            .Select(s => new LiveDataSnapshotDto {
                Id = s.Id,
                Timestamp = s.Timestamp,
                PlaySessionId = s.PlaySessionId,
                Score = s.Score,
                ScoreWithMultipliers = s.ScoreWithMultipliers,
                MaxScore = s.MaxScore,
                MaxScoreWithMultipliers = s.MaxScoreWithMultipliers,
                Rank = s.Rank,
                FullCombo = s.FullCombo,
                NotesSpawned = s.NotesSpawned,
                Combo = s.Combo,
                Misses = s.Misses,
                Accuracy = s.Accuracy,
                PlayerHealth = s.PlayerHealth,
                TimeElapsed = s.TimeElapsed,
                EventTrigger = s.EventTrigger,
                BlockHitPreSwing = s.BlockHitPreSwing,
                BlockHitPostSwing = s.BlockHitPostSwing,
                BlockHitCenterSwing = s.BlockHitCenterSwing,
                NoteColorType = s.NoteColorType
            })
            .ToListAsync();
    }

    public async Task<SessionPerformanceBreakdownDto?> GetSessionPerformanceBreakdownAsync(long sessionId) {
        var session = await _context.PlaySessions.FindAsync(sessionId);
        if (session == null) return null;

        var snapshots = await GetSnapshotsBySessionIdAsync(sessionId);

        return new SessionPerformanceBreakdownDto {
            PlaySessionId = sessionId,
            Snapshots = snapshots,
            AccuracyOverTime = new AccuracyOverTimeDto {
                Points = snapshots.Select(s => new TimePointDto {
                    TimeElapsed = s.TimeElapsed,
                    Value = s.Accuracy
                }).ToList()
            },
            ScoreOverTime = new ScoreOverTimeDto {
                Points = snapshots.Select(s => new TimePointDto {
                    TimeElapsed = s.TimeElapsed,
                    Value = s.ScorePercentage
                }).ToList()
            },
            HealthOverTime = new HealthOverTimeDto {
                Points = snapshots.Select(s => new TimePointDto {
                    TimeElapsed = s.TimeElapsed,
                    Value = s.PlayerHealth
                }).ToList()
            },
            ComboOverTime = new ComboOverTimeDto {
                Points = snapshots.Select(s => new ComboPointDto {
                    TimeElapsed = s.TimeElapsed,
                    Combo = s.Combo,
                    Misses = s.Misses
                }).ToList()
            }
        };
    }

    public async Task<AnalyticsOverviewDto> GetAnalyticsOverviewAsync() {
        var sessions = await _context.PlaySessions
            .Include(s => s.Map)
            .Include(s => s.Difficulty)
            .ToListAsync();

        if (sessions.Count == 0) {
            return new AnalyticsOverviewDto();
        }

        var finishedSessions = sessions.Where(s => s.FinishedAt.HasValue).ToList();
        var totalPlayTime = finishedSessions.Sum(s => (s.FinishedAt!.Value - s.StartedAt).TotalSeconds);

        var bestSession = sessions
            .Where(s => s.FinalMaxScore > 0)
            .OrderByDescending(s => (double) s.FinalScore / s.FinalMaxScore)
            .FirstOrDefault();

        var mostRecentSession = sessions.OrderByDescending(s => s.StartedAt).FirstOrDefault();

        return new AnalyticsOverviewDto {
            TotalPlaySessions = sessions.Count,
            TotalMapsPlayed = sessions.Select(s => s.MapId).Distinct().Count(),
            UniqueMaps = sessions.Select(s => s.MapId).Distinct().Count(),
            TotalPlayTimeSeconds = (int) totalPlayTime,
            AverageAccuracy = sessions.Where(s => s.FinalAccuracy > 0).DefaultIfEmpty().Average(s => s?.FinalAccuracy ?? 0),
            AverageScorePercentage = sessions.Where(s => s.FinalMaxScore > 0)
                .Select(s => (double) s.FinalScore / s.FinalMaxScore * 100)
                .DefaultIfEmpty(0)
                .Average(),
            FullComboCount = sessions.Count(s => s.FinalFullCombo),
            FinishedCount = sessions.Count(s => s.EndReason == "Finished"),
            FailedCount = sessions.Count(s => s.EndReason == "Failed"),
            QuitCount = sessions.Count(s => s.EndReason == "Quit"),
            SSSCount = sessions.Count(s => s.FinalRank == "SSS"),
            SCount = sessions.Count(s => s.FinalRank == "S"),
            ACount = sessions.Count(s => s.FinalRank == "A"),
            BCount = sessions.Count(s => s.FinalRank == "B"),
            CCount = sessions.Count(s => s.FinalRank == "C"),
            DCount = sessions.Count(s => s.FinalRank == "D"),
            ECount = sessions.Count(s => s.FinalRank == "E"),
            BestSession = bestSession != null ? MapToSummaryDto(bestSession) : null,
            MostRecentSession = mostRecentSession != null ? MapToSummaryDto(mostRecentSession) : null,
            FirstPlayDate = sessions.Min(s => s.StartedAt),
            LastPlayDate = sessions.Max(s => s.StartedAt)
        };
    }

    public async Task<List<PerformanceTrendDto>> GetPerformanceTrendAsync(PerformanceTrendFilterParams filter) {
        var query = _context.PlaySessions.AsQueryable();

        if (filter.FromDate.HasValue) {
            query = query.Where(s => s.StartedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue) {
            query = query.Where(s => s.StartedAt <= filter.ToDate.Value);
        }

        var sessions = await query.ToListAsync();

        var groupByLower = filter.GroupBy.ToLower();
        IEnumerable<IGrouping<object, PlaySessionEntity>> grouped = groupByLower switch {
            "hour" => sessions.GroupBy(s => (object) new { s.StartedAt.Year, s.StartedAt.Month, s.StartedAt.Day, s.StartedAt.Hour }),
            "week" => sessions.GroupBy(s => (object) new { Year = s.StartedAt.Year, Week = GetWeekOfYear(s.StartedAt.DateTime) }),
            "month" => sessions.GroupBy(s => (object) new { s.StartedAt.Year, s.StartedAt.Month }),
            _ => sessions.GroupBy(s => (object) new { s.StartedAt.Year, s.StartedAt.Month, s.StartedAt.Day })
        };

        return grouped.Select(g => {
            var sessionsList = g.ToList();
            var finishedSessions = sessionsList.Where(s => s.FinishedAt.HasValue).ToList();
            var totalPlayTime = finishedSessions.Sum(s => (s.FinishedAt!.Value - s.StartedAt).TotalSeconds);

            var date = GetDateFromGroupKey(g.Key, groupByLower);

            return new PerformanceTrendDto {
                Date = date,
                PlayCount = sessionsList.Count,
                AverageAccuracy = sessionsList.Where(s => s.FinalAccuracy > 0).DefaultIfEmpty().Average(s => s?.FinalAccuracy ?? 0),
                AverageScorePercentage = sessionsList.Where(s => s.FinalMaxScore > 0)
                    .Select(s => (double) s.FinalScore / s.FinalMaxScore * 100)
                    .DefaultIfEmpty(0)
                    .Average(),
                TotalPlayTimeSeconds = (int) totalPlayTime,
                FullComboCount = sessionsList.Count(s => s.FinalFullCombo),
                FinishedCount = sessionsList.Count(s => s.EndReason == "Finished"),
                FailedCount = sessionsList.Count(s => s.EndReason == "Failed")
            };
        })
        .OrderBy(t => t.Date)
        .ToList();
    }

    public async Task<List<MapPlayStatsDto>> GetTopPlayedMapsAsync(int count = 10) {
        var mapStats = await _context.Maps
            .Include(m => m.PlaySessions)
            .Where(m => m.PlaySessions.Any())
            .Select(m => new {
                Map = m,
                PlayCount = m.PlaySessions.Count,
                AvgAccuracy = m.PlaySessions.Average(s => s.FinalAccuracy),
                AvgScorePercentage = m.PlaySessions.Where(s => s.FinalMaxScore > 0)
                    .Select(s => (double) s.FinalScore / s.FinalMaxScore * 100)
                    .DefaultIfEmpty(0)
                    .Average(),
                FullComboCount = m.PlaySessions.Count(s => s.FinalFullCombo),
                BestScore = m.PlaySessions.Max(s => s.FinalScore),
                BestScorePercentage = m.PlaySessions.Where(s => s.FinalMaxScore > 0)
                    .Select(s => (double) s.FinalScore / s.FinalMaxScore * 100)
                    .DefaultIfEmpty(0)
                    .Max(),
                BestRank = m.PlaySessions.OrderBy(s => GetRankValue(s.FinalRank)).Select(s => s.FinalRank).FirstOrDefault(),
                LastPlayedAt = m.PlaySessions.Max(s => s.StartedAt)
            })
            .OrderByDescending(m => m.PlayCount)
            .Take(count)
            .ToListAsync();

        return mapStats.Select(m => new MapPlayStatsDto {
            MapId = m.Map.Id,
            SongName = m.Map.SongName,
            SongAuthor = m.Map.SongAuthor,
            Mapper = m.Map.Mapper,
            HasCoverImage = m.Map.CoverImage != null,
            BSRKey = m.Map.BSRKey,
            PlayCount = m.PlayCount,
            AverageAccuracy = m.AvgAccuracy,
            AverageScorePercentage = m.AvgScorePercentage,
            FullComboCount = m.FullComboCount,
            BestScore = m.BestScore,
            BestScorePercentage = m.BestScorePercentage,
            BestRank = m.BestRank,
            LastPlayedAt = m.LastPlayedAt
        }).ToList();
    }

    public async Task<List<MapPlayStatsDto>> GetBestPerformingMapsAsync(int count = 10) {
        var mapStats = await _context.Maps
            .Include(m => m.PlaySessions)
            .Where(m => m.PlaySessions.Any(s => s.FinalMaxScore > 0))
            .Select(m => new {
                Map = m,
                PlayCount = m.PlaySessions.Count,
                AvgAccuracy = m.PlaySessions.Average(s => s.FinalAccuracy),
                AvgScorePercentage = m.PlaySessions.Where(s => s.FinalMaxScore > 0)
                    .Select(s => (double) s.FinalScore / s.FinalMaxScore * 100)
                    .DefaultIfEmpty(0)
                    .Average(),
                FullComboCount = m.PlaySessions.Count(s => s.FinalFullCombo),
                BestScore = m.PlaySessions.Max(s => s.FinalScore),
                BestScorePercentage = m.PlaySessions.Where(s => s.FinalMaxScore > 0)
                    .Select(s => (double) s.FinalScore / s.FinalMaxScore * 100)
                    .DefaultIfEmpty(0)
                    .Max(),
                BestRank = m.PlaySessions.OrderBy(s => GetRankValue(s.FinalRank)).Select(s => s.FinalRank).FirstOrDefault(),
                LastPlayedAt = m.PlaySessions.Max(s => s.StartedAt)
            })
            .Where(m => m.AvgScorePercentage > 0)
            .OrderByDescending(m => m.AvgScorePercentage)
            .Take(count)
            .ToListAsync();

        return mapStats.Select(m => new MapPlayStatsDto {
            MapId = m.Map.Id,
            SongName = m.Map.SongName,
            SongAuthor = m.Map.SongAuthor,
            Mapper = m.Map.Mapper,
            HasCoverImage = m.Map.CoverImage != null,
            BSRKey = m.Map.BSRKey,
            PlayCount = m.PlayCount,
            AverageAccuracy = m.AvgAccuracy,
            AverageScorePercentage = m.AvgScorePercentage,
            FullComboCount = m.FullComboCount,
            BestScore = m.BestScore,
            BestScorePercentage = m.BestScorePercentage,
            BestRank = m.BestRank,
            LastPlayedAt = m.LastPlayedAt
        }).ToList();
    }

    public async Task<List<DifficultyStatsDto>> GetDifficultyStatsAsync() {
        var sessions = await _context.PlaySessions
            .Include(s => s.Difficulty)
            .ToListAsync();

        return sessions
            .GroupBy(s => s.Difficulty.Difficulty)
            .Select(g => new DifficultyStatsDto {
                Difficulty = g.Key,
                PlayCount = g.Count(),
                AverageAccuracy = g.Where(s => s.FinalAccuracy > 0).DefaultIfEmpty().Average(s => s?.FinalAccuracy ?? 0),
                AverageScorePercentage = g.Where(s => s.FinalMaxScore > 0)
                    .Select(s => (double) s.FinalScore / s.FinalMaxScore * 100)
                    .DefaultIfEmpty(0)
                    .Average(),
                FullComboCount = g.Count(s => s.FinalFullCombo),
                FinishedCount = g.Count(s => s.EndReason == "Finished"),
                FailedCount = g.Count(s => s.EndReason == "Failed")
            })
            .OrderByDescending(d => d.PlayCount)
            .ToList();
    }

    public async Task<List<MapTypeStatsDto>> GetMapTypeStatsAsync() {
        var sessions = await _context.PlaySessions
            .Include(s => s.Difficulty)
            .ToListAsync();

        return sessions
            .GroupBy(s => s.Difficulty.MapType)
            .Select(g => new MapTypeStatsDto {
                MapType = g.Key,
                PlayCount = g.Count(),
                AverageAccuracy = g.Where(s => s.FinalAccuracy > 0).DefaultIfEmpty().Average(s => s?.FinalAccuracy ?? 0),
                AverageScorePercentage = g.Where(s => s.FinalMaxScore > 0)
                    .Select(s => (double) s.FinalScore / s.FinalMaxScore * 100)
                    .DefaultIfEmpty(0)
                    .Average(),
                FullComboCount = g.Count(s => s.FinalFullCombo)
            })
            .OrderByDescending(m => m.PlayCount)
            .ToList();
    }

    public async Task<List<ScoreDistributionDto>> GetScoreDistributionAsync() {
        var sessions = await _context.PlaySessions
            .Where(s => s.FinalRank != null)
            .ToListAsync();

        var total = sessions.Count;
        if (total == 0) return [];

        var ranks = new[] { "SSS", "S", "A", "B", "C", "D", "E" };

        return ranks.Select(rank => {
            var count = sessions.Count(s => s.FinalRank == rank);
            return new ScoreDistributionDto {
                Rank = rank,
                Count = count,
                Percentage = (double) count / total * 100
            };
        }).ToList();
    }

    public async Task<List<HourlyPlayStatsDto>> GetHourlyPlayStatsAsync() {
        var sessions = await _context.PlaySessions.ToListAsync();

        return Enumerable.Range(0, 24)
            .Select(hour => {
                var hourSessions = sessions.Where(s => s.StartedAt.Hour == hour).ToList();
                return new HourlyPlayStatsDto {
                    Hour = hour,
                    PlayCount = hourSessions.Count,
                    AverageAccuracy = hourSessions.Where(s => s.FinalAccuracy > 0).DefaultIfEmpty().Average(s => s?.FinalAccuracy ?? 0)
                };
            })
            .ToList();
    }

    public async Task<List<DailyPlayStatsDto>> GetDailyPlayStatsAsync() {
        var sessions = await _context.PlaySessions.ToListAsync();
        var days = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        return Enumerable.Range(0, 7)
            .Select(day => {
                var daySessions = sessions.Where(s => (int) s.StartedAt.DayOfWeek == day).ToList();
                return new DailyPlayStatsDto {
                    DayOfWeek = day,
                    DayName = days[day],
                    PlayCount = daySessions.Count,
                    AverageAccuracy = daySessions.Where(s => s.FinalAccuracy > 0).DefaultIfEmpty().Average(s => s?.FinalAccuracy ?? 0)
                };
            })
            .ToList();
    }

    public async Task<List<AccuracyDistributionDto>> GetAccuracyDistributionAsync() {
        var sessions = await _context.PlaySessions
            .Where(s => s.FinalAccuracy > 0)
            .ToListAsync();

        if (sessions.Count == 0) return [];

        var ranges = new[] {
            (Min: 95, Max: 100, Label: "95-100%"),
            (Min: 90, Max: 95, Label: "90-95%"),
            (Min: 85, Max: 90, Label: "85-90%"),
            (Min: 80, Max: 85, Label: "80-85%"),
            (Min: 75, Max: 80, Label: "75-80%"),
            (Min: 70, Max: 75, Label: "70-75%"),
            (Min: 60, Max: 70, Label: "60-70%"),
            (Min: 50, Max: 60, Label: "50-60%"),
            (Min: 0, Max: 50, Label: "<50%")
        };

        var total = sessions.Count;

        return ranges.Select(range => {
            var count = sessions.Count(s => s.FinalAccuracy >= range.Min && s.FinalAccuracy < range.Max);
            return new AccuracyDistributionDto {
                Range = range.Label,
                Count = count,
                Percentage = (double) count / total * 100
            };
        }).ToList();
    }

    private static MapDto MapToDto(MapEntity map) {
        return new MapDto {
            Id = map.Id,
            Hash = map.Hash,
            SongName = map.SongName,
            SongSubName = map.SongSubName,
            SongAuthor = map.SongAuthor,
            Mapper = map.Mapper,
            BSRKey = map.BSRKey,
            Duration = map.Duration,
            BPM = map.BPM,
            HasCoverImage = map.CoverImage != null,
            GameVersion = map.GameVersion,
            PlayCount = map.PlaySessions?.Count ?? 0,
            Difficulties = map.Difficulties?.Select(d => new DifficultyDto {
                Id = d.Id,
                MapId = d.MapId,
                MapType = d.MapType,
                Difficulty = d.Difficulty,
                CustomDifficultyLabel = d.CustomDifficultyLabel,
                NJS = d.NJS,
                PP = d.PP,
                Star = d.Star,
                PlayCount = d.PlaySessions?.Count ?? 0
            }).ToList() ?? []
        };
    }

    private static PlaySessionDto SessionToDto(PlaySessionEntity session) {
        return new PlaySessionDto {
            Id = session.Id,
            StartedAt = session.StartedAt,
            FinishedAt = session.FinishedAt,
            EndReason = session.EndReason,
            Map = new MapSummaryDto {
                Id = session.Map.Id,
                Hash = session.Map.Hash,
                SongName = session.Map.SongName,
                SongSubName = session.Map.SongSubName,
                SongAuthor = session.Map.SongAuthor,
                Mapper = session.Map.Mapper,
                BSRKey = session.Map.BSRKey,
                Duration = session.Map.Duration,
                BPM = session.Map.BPM,
                HasCoverImage = session.Map.CoverImage != null,
                PlayCount = session.Map.PlaySessions?.Count ?? 0,
                DifficultyCount = session.Map.Difficulties?.Count ?? 0
            },
            Difficulty = new DifficultyDto {
                Id = session.Difficulty.Id,
                MapId = session.Difficulty.MapId,
                MapType = session.Difficulty.MapType,
                Difficulty = session.Difficulty.Difficulty,
                CustomDifficultyLabel = session.Difficulty.CustomDifficultyLabel,
                NJS = session.Difficulty.NJS,
                PP = session.Difficulty.PP,
                Star = session.Difficulty.Star,
                PlayCount = session.Difficulty.PlaySessions?.Count ?? 0
            },
            ModifiersMultiplier = session.ModifiersMultiplier,
            PracticeMode = session.PracticeMode,
            PluginVersion = session.PluginVersion,
            IsMultiplayer = session.IsMultiplayer,
            PreviousRecord = session.PreviousRecord,
            PreviousBSR = session.PreviousBSR,
            Modifiers = new ModifiersDto {
                NoFailOn0Energy = session.Modifiers.NoFailOn0Energy,
                OneLife = session.Modifiers.OneLife,
                FourLives = session.Modifiers.FourLives,
                NoBombs = session.Modifiers.NoBombs,
                NoWalls = session.Modifiers.NoWalls,
                NoArrows = session.Modifiers.NoArrows,
                GhostNotes = session.Modifiers.GhostNotes,
                DisappearingArrows = session.Modifiers.DisappearingArrows,
                SmallNotes = session.Modifiers.SmallNotes,
                ProMode = session.Modifiers.ProMode,
                StrictAngles = session.Modifiers.StrictAngles,
                ZenMode = session.Modifiers.ZenMode,
                SlowerSong = session.Modifiers.SlowerSong,
                FasterSong = session.Modifiers.FasterSong,
                SuperFastSong = session.Modifiers.SuperFastSong
            },
            PracticeModeModifiers = new PracticeModeModifiersDto {
                SongSpeedMul = session.PracticeModeModifiers.SongSpeedMul,
                StartInAdvanceAndClearNotes = session.PracticeModeModifiers.StartInAdvanceAndClearNotes,
                SongStartTime = session.PracticeModeModifiers.SongStartTime
            },
            FinalScore = session.FinalScore,
            FinalScoreWithMultipliers = session.FinalScoreWithMultipliers,
            FinalMaxScore = session.FinalMaxScore,
            FinalMaxScoreWithMultipliers = session.FinalMaxScoreWithMultipliers,
            FinalRank = session.FinalRank,
            FinalFullCombo = session.FinalFullCombo,
            FinalCombo = session.FinalCombo,
            FinalMisses = session.FinalMisses,
            FinalAccuracy = session.FinalAccuracy,
            FinalTimeElapsed = session.FinalTimeElapsed
        };
    }

    private static PlaySessionSummaryDto MapToSummaryDto(PlaySessionEntity session) {
        return new PlaySessionSummaryDto {
            Id = session.Id,
            StartedAt = session.StartedAt,
            FinishedAt = session.FinishedAt,
            EndReason = session.EndReason,
            MapId = session.MapId,
            SongName = session.Map?.SongName ?? "",
            SongAuthor = session.Map?.SongAuthor ?? "",
            Mapper = session.Map?.Mapper ?? "",
            HasCoverImage = session.Map?.CoverImage != null,
            Difficulty = session.Difficulty?.Difficulty ?? "",
            MapType = session.Difficulty?.MapType ?? "",
            FinalScore = session.FinalScore,
            FinalMaxScore = session.FinalMaxScore,
            FinalRank = session.FinalRank,
            FinalAccuracy = session.FinalAccuracy,
            FinalFullCombo = session.FinalFullCombo,
            FinalMisses = session.FinalMisses
        };
    }

    private static IQueryable<MapEntity> ApplySorting(IQueryable<MapEntity> query, string? sortBy, bool descending) {
        return sortBy?.ToLower() switch {
            "songname" => descending ? query.OrderByDescending(m => m.SongName) : query.OrderBy(m => m.SongName),
            "songauthor" => descending ? query.OrderByDescending(m => m.SongAuthor) : query.OrderBy(m => m.SongAuthor),
            "mapper" => descending ? query.OrderByDescending(m => m.Mapper) : query.OrderBy(m => m.Mapper),
            "bpm" => descending ? query.OrderByDescending(m => m.BPM) : query.OrderBy(m => m.BPM),
            "duration" => descending ? query.OrderByDescending(m => m.Duration) : query.OrderBy(m => m.Duration),
            "id" => descending ? query.OrderByDescending(m => m.Id) : query.OrderBy(m => m.Id),
            _ => descending ? query.OrderByDescending(m => m.Id) : query.OrderBy(m => m.Id)
        };
    }

    private static IQueryable<PlaySessionEntity> ApplySessionSorting(IQueryable<PlaySessionEntity> query, string? sortBy, bool descending) {
        return sortBy?.ToLower() switch {
            "startedat" => descending ? query.OrderByDescending(s => s.Id) : query.OrderBy(s => s.Id),
            "finishedat" => descending ? query.OrderByDescending(s => s.Id) : query.OrderBy(s => s.Id),
            "score" => descending ? query.OrderByDescending(s => s.FinalScore) : query.OrderBy(s => s.FinalScore),
            "accuracy" => descending ? query.OrderByDescending(s => s.FinalAccuracy) : query.OrderBy(s => s.FinalAccuracy),
            "id" => descending ? query.OrderByDescending(s => s.Id) : query.OrderBy(s => s.Id),
            _ => descending ? query.OrderByDescending(s => s.Id) : query.OrderBy(s => s.Id)
        };
    }

    private static int GetRankValue(string? rank) {
        return rank?.ToUpper() switch {
            "SSS" => 0,
            "S" => 1,
            "A" => 2,
            "B" => 3,
            "C" => 4,
            "D" => 5,
            "E" => 6,
            _ => 99
        };
    }

    private static int GetWeekOfYear(DateTime date) {
        var dayOfYear = date.DayOfYear;
        return (dayOfYear - 1) / 7 + 1;
    }

    private static DateTimeOffset GetDateFromGroupKey(object key, string groupBy) {
        var type = key.GetType();
        var year = (int) type.GetProperty("Year")!.GetValue(key)!;

        if (groupBy == "hour") {
            var month = (int) type.GetProperty("Month")!.GetValue(key)!;
            var day = (int) type.GetProperty("Day")!.GetValue(key)!;
            var hour = (int) type.GetProperty("Hour")!.GetValue(key)!;
            return new DateTimeOffset(year, month, day, hour, 0, 0, TimeSpan.Zero);
        }

        if (groupBy == "week") {
            var week = (int) type.GetProperty("Week")!.GetValue(key)!;
            return new DateTimeOffset(year, 1, 1 + (week - 1) * 7, 0, 0, 0, TimeSpan.Zero);
        }

        if (groupBy == "month") {
            var month = (int) type.GetProperty("Month")!.GetValue(key)!;
            return new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        }

        var dayOfMonth = (int) type.GetProperty("Day")!.GetValue(key)!;
        var monthOfYear = (int) type.GetProperty("Month")!.GetValue(key)!;
        return new DateTimeOffset(year, monthOfYear, dayOfMonth, 0, 0, 0, TimeSpan.Zero);
    }

    public async Task<DashboardDto> GetDashboardAsync() {
        var overview = await GetAnalyticsOverviewAsync();
        var recentSessions = await GetRecentSessionsAsync(5);
        var topPlayedMaps = await GetTopPlayedMapsAsync(5);
        var scoreDistribution = await GetScoreDistributionAsync();
        var dailyStats = await GetDailyPlayStatsAsync();

        var recentTrend = await GetPerformanceTrendAsync(new PerformanceTrendFilterParams {
            FromDate = DateTimeOffset.UtcNow.AddDays(-30),
            GroupBy = "day"
        });

        return new DashboardDto {
            Overview = overview,
            RecentSessions = recentSessions,
            TopPlayedMaps = topPlayedMaps,
            RecentTrend = recentTrend.Take(30).ToList(),
            ScoreDistribution = scoreDistribution,
            DailyStats = dailyStats
        };
    }

    public async Task<MapDetailDto?> GetMapDetailAsync(long mapId) {
        var map = await GetMapByIdAsync(mapId);
        if (map == null) return null;

        var sessions = await GetSessionsByMapIdAsync(mapId, 50);
        var allDifficultyStats = await GetDifficultyStatsAsync();

        var mapDifficultyStats = allDifficultyStats
            .Where(d => map.Difficulties.Any(md => md.Difficulty == d.Difficulty))
            .ToList();

        var bestSession = sessions
            .Where(s => s.FinalMaxScore > 0)
            .OrderByDescending(s => (double) s.FinalScore / s.FinalMaxScore)
            .FirstOrDefault();

        MapPlayStatsDto? stats = null;
        if (sessions.Count > 0) {
            stats = new MapPlayStatsDto {
                MapId = map.Id,
                SongName = map.SongName,
                SongAuthor = map.SongAuthor,
                Mapper = map.Mapper,
                HasCoverImage = map.HasCoverImage,
                BSRKey = map.BSRKey,
                PlayCount = map.PlayCount,
                AverageAccuracy = sessions.Average(s => s.FinalAccuracy),
                AverageScorePercentage = sessions.Where(s => s.FinalMaxScore > 0)
                    .Select(s => (double) s.FinalScore / s.FinalMaxScore * 100)
                    .DefaultIfEmpty(0)
                    .Average(),
                FullComboCount = sessions.Count(s => s.FinalFullCombo),
                BestScore = sessions.Max(s => s.FinalScore),
                BestScorePercentage = sessions.Where(s => s.FinalMaxScore > 0)
                    .Select(s => (double) s.FinalScore / s.FinalMaxScore * 100)
                    .DefaultIfEmpty(0)
                    .Max(),
                BestRank = bestSession?.FinalRank,
                LastPlayedAt = sessions.Max(s => s.StartedAt)
            };
        }

        PerformanceTrendDto? bestPerformance = null;
        if (bestSession != null) {
            bestPerformance = new PerformanceTrendDto {
                Date = bestSession.StartedAt,
                PlayCount = 1,
                AverageAccuracy = bestSession.FinalAccuracy,
                AverageScorePercentage = bestSession.ScorePercentage,
                TotalPlayTimeSeconds = 0,
                FullComboCount = bestSession.FinalFullCombo ? 1 : 0,
                FinishedCount = bestSession.EndReason == "Finished" ? 1 : 0,
                FailedCount = bestSession.EndReason == "Failed" ? 1 : 0
            };
        }

        return new MapDetailDto {
            Map = map,
            Stats = stats,
            RecentSessions = sessions.Take(10).ToList(),
            DifficultyStats = mapDifficultyStats,
            BestPerformance = bestPerformance
        };
    }

    public async Task<SessionDetailDto?> GetSessionDetailAsync(long sessionId) {
        var session = await GetPlaySessionByIdAsync(sessionId);
        if (session == null) return null;

        var performance = await GetSessionPerformanceBreakdownAsync(sessionId);
        if (performance == null) return null;

        var snapshots = performance.Snapshots;

        return new SessionDetailDto {
            Session = session,
            Performance = performance,
            SnapshotCount = snapshots.Count,
            MissCount = snapshots.Count > 0 ? snapshots.Max(s => s.Misses) : session.FinalMisses,
            MaxCombo = snapshots.Count > 0 ? snapshots.Max(s => s.Combo) : session.FinalCombo,
            AverageHealth = snapshots.Count > 0 ? snapshots.Average(s => s.PlayerHealth) : 50
        };
    }

    public async Task<string?> GetMapCoverImageAsync(long mapId) {
        var map = await _context.Maps.FindAsync(mapId);
        return map?.CoverImage;
    }

    public async Task<string?> GetMapCoverImageByHashAsync(string hash) {
        var map = await _context.Maps.FirstOrDefaultAsync(m => m.Hash == hash);
        return map?.CoverImage;
    }

    public async Task<string?> GetMapCoverImageByBSRKeyAsync(string bsrKey) {
        var map = await _context.Maps.FirstOrDefaultAsync(m => m.BSRKey == bsrKey);
        return map?.CoverImage;
    }
}

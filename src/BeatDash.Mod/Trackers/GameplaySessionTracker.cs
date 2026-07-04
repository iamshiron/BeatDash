using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Shiron.BeatDash.Data.Socket;
using Shiron.BeatDash.Mod.Network;
using SiraUtil.Zenject;
using SongCore;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shiron.BeatDash.Mod.Trackers;

[UsedImplicitly]
public sealed class GameplaySessionTracker(
    GameplaySession session,
    GameplayCoreSceneSetupData setupData,
    PauseController pauseController,
    ILevelEndActions levelEndActions,
    PrepareLevelCompletionResults prepareResults,
    NetworkManager networkManager
) : IAsyncInitializable, IDisposable {
    private static readonly TimeSpan CorrelationAssignmentTimeout = TimeSpan.FromSeconds(5);

    public async Task InitializeAsync(CancellationToken ct) {
        var level = setupData.beatmapLevel;
        var key = setupData.beatmapKey;
        var basicData = setupData.beatmapBasicData;
        var transformedData = setupData.transformedBeatmapData;
        var characteristic = key.beatmapCharacteristic;

        session.LevelId = level.levelID;
        session.MaxMultipliedScore = ScoreModel.ComputeMaxMultipliedScoreForBeatmap(transformedData);

        var coverPng = await GetCoverPngAsync(level);

        var (notesLeft, notesRight, npsCurve, walls, bombs) = ComputeMapDetails(transformedData, level.songDuration);
        var songSpeedMul = setupData.gameplayModifiers.songSpeed switch {
            GameplayModifiers.SongSpeed.Slower => 0.85f,
            GameplayModifiers.SongSpeed.Faster => 1.2f,
            GameplayModifiers.SongSpeed.SuperFast => 1.5f,
            _ => 1f
        };

        var mapPayload = new MapStartMessage {
            CorrelationId = 0,
            LevelId = level.levelID,
            DurationMs = (int) (level.songDuration * 1000f),
            NotesPerSecond = transformedData.cuttableNotesCount / level.songDuration,
            SongName = level.songName,
            SongSubName = level.songSubName,
            SongAuthor = level.songAuthorName,
            Mapper = level.allMappers.Length > 0
                ? string.Join(", ", level.allMappers)
                : level.songAuthorName ?? "Unknown",
            Bpm = level.beatsPerMinute,
            Difficulty = key.difficulty.SerializedName(),
            DifficultyName = ExtractDifficultyName(level, key),
            NoteJumpSpeed = GetNoteJumpSpeed(key.difficulty, basicData),
            BombCount = transformedData.bombsCount,
            ObstacleCount = transformedData.obstaclesCount,
            CuttableObjectCount = transformedData.cuttableNotesCount,
            LaneCount = transformedData.numberOfLines,
            Characteristic = new BeatmapCharacteristic {
                SerializedName = characteristic.serializedName,
                ContainsRotationEvents = characteristic.containsRotationEvents,
                DescriptionLocalizationKey = characteristic.descriptionLocalizationKey,
                LocalizationKey = characteristic.characteristicNameLocalizationKey,
                ColorCount = characteristic.numberOfColors,
                Requires360Movement = characteristic.requires360Movement
            },
            ModifierFlags = PackModifierFlags(setupData.gameplayModifiers),
            SongSpeed = songSpeedMul,
            NotesPerHandLeft = notesLeft,
            NotesPerHandRight = notesRight,
            NpsCurve = npsCurve,
            WallTimeline = walls,
            BombPositions = bombs
        };

        Plugin.Log.Info($"Sending map data: {mapPayload.SongName} - {mapPayload.SongAuthor}, awaiting server-assigned correlation ID...");
        networkManager.PrepareCorrelationAssignment();
        await networkManager.PostJsonBinaryAsync(BinaryPacketTypes.MapStart, mapPayload, forceTcp: true);

        var assigned = await networkManager.WaitForCorrelationAssignmentAsync(CorrelationAssignmentTimeout, ct);
        if (assigned is null) {
            Plugin.Log.Error("Timed out waiting for server-assigned correlation ID; telemetry disabled for this map.");
            return;
        }

        session.CorrelationId = assigned.Value;
        session.IsInitialized = true;

        pauseController.didPauseEvent += HandlePaused;
        pauseController.didResumeEvent += HandleResumed;
        pauseController.didReturnToMenuEvent += HandleQuit;
        levelEndActions.levelFinishedEvent += HandleFinished;
        levelEndActions.levelFailedEvent += HandleFailed;

        var imageData = MapCoverImagePacket.Build(session.CorrelationId, coverPng);
        await networkManager.PostBinaryAsync(BinaryPacketTypes.MapCoverImage, imageData, forceTcp: true);

        Plugin.Log.Info($"Map data sent: {mapPayload.SongName} - {mapPayload.SongAuthor} ({imageData.Length} bytes) [corr={session.CorrelationId}]");
    }

    private async void HandlePaused() {
        await SendStateAsync(MapState.Paused);
    }

    private async void HandleResumed() {
        await SendStateAsync(MapState.Resumed);
    }

    private async void HandleFinished() {
        var results = prepareResults.FillLevelCompletionResults(
            LevelCompletionResults.LevelEndStateType.Cleared,
            LevelCompletionResults.LevelEndAction.None);
        await SendStateAsync(MapState.Finished, results);
    }

    private async void HandleFailed() {
        var results = prepareResults.FillLevelCompletionResults(
            LevelCompletionResults.LevelEndStateType.Failed,
            LevelCompletionResults.LevelEndAction.None);
        await SendStateAsync(MapState.Failed, results);
    }

    private async void HandleQuit() {
        var results = prepareResults.FillLevelCompletionResults(
            LevelCompletionResults.LevelEndStateType.Incomplete,
            LevelCompletionResults.LevelEndAction.Quit);
        await SendStateAsync(MapState.Quit, results);
    }

    private async Task SendStateAsync(MapState state, LevelCompletionResults? bsResults = null) {
        try {
            var message = new MapStateMessage {
                CorrelationId = session.CorrelationId,
                LevelId = session.LevelId,
                State = state.ToString(),
                Results = bsResults is not null ? ToMapResults(bsResults) : null
            };

            Plugin.Log.Info($"Sending map state: {state} [corr={session.CorrelationId}]");
            await networkManager.PostJsonBinaryAsync(BinaryPacketTypes.MapState, message, forceTcp: true);
        } catch (Exception e) {
            Plugin.Log.Error($"Failed to send MapState.{state}: {e.Message}");
        }
    }

    private MapResults ToMapResults(LevelCompletionResults results) {
        var accuracy = session.MaxMultipliedScore > 0
            ? results.multipliedScore / (float) session.MaxMultipliedScore
            : 0f;

        return new MapResults {
            Score = results.modifiedScore,
            MultipliedScore = results.multipliedScore,
            MaxMultipliedScore = session.MaxMultipliedScore,
            Accuracy = accuracy,
            Rank = results.rank.ToString(),
            FullCombo = results.fullCombo,
            MaxCombo = results.maxCombo,
            GoodCuts = results.goodCutsCount,
            BadCuts = results.badCutsCount,
            MissedNotes = results.missedCount,
            Energy = results.energy,
            EndSongTime = results.endSongTime
        };
    }

    private static (int NotesLeft, int NotesRight, int[] NpsCurve, WallEntryDto[] Walls, BombEntryDto[] Bombs)
        ComputeMapDetails(IReadonlyBeatmapData data, float songDuration) {

        var bucketCount = Math.Max(1, (int) Math.Ceiling(songDuration) + 1);
        var nps = new int[bucketCount];
        var walls = new List<WallEntryDto>();
        var bombs = new List<BombEntryDto>();
        var notesLeft = 0;
        var notesRight = 0;

        foreach (var item in data.allBeatmapDataItems) {
            switch (item) {
                case NoteData note when note.gameplayType == NoteData.GameplayType.Bomb:
                    bombs.Add(new BombEntryDto {
                        SongTime = note.time,
                        LineIndex = note.lineIndex,
                        NoteLineLayer = (int) note.noteLineLayer
                    });
                    break;
                case NoteData note:
                    if (note.colorType == ColorType.ColorA) notesLeft++;
                    else if (note.colorType == ColorType.ColorB) notesRight++;
                    var bucket = (int) note.time;
                    if (bucket >= 0 && bucket < nps.Length) nps[bucket]++;
                    break;
                case ObstacleData obstacle:
                    walls.Add(new WallEntryDto {
                        StartTime = obstacle.time,
                        Duration = obstacle.duration,
                        LineIndex = obstacle.lineIndex,
                        Width = obstacle.width,
                        Height = obstacle.height
                    });
                    break;
            }
        }

        return (notesLeft, notesRight, nps, walls.ToArray(), bombs.ToArray());
    }

    private static int PackModifierFlags(GameplayModifiers gm) {
        var flags = 0;

        if (gm.noFailOn0Energy) flags |= 1 << (int) ModifierBit.NoFailOn0Energy;
        if (gm.instaFail) flags |= 1 << (int) ModifierBit.InstaFail;
        if (gm.failOnSaberClash) flags |= 1 << (int) ModifierBit.FailOnSaberClash;
        if (gm.noBombs) flags |= 1 << (int) ModifierBit.NoBombs;
        if (gm.fastNotes) flags |= 1 << (int) ModifierBit.FastNotes;
        if (gm.strictAngles) flags |= 1 << (int) ModifierBit.StrictAngles;
        if (gm.disappearingArrows) flags |= 1 << (int) ModifierBit.DisappearingArrows;
        if (gm.ghostNotes) flags |= 1 << (int) ModifierBit.GhostNotes;
        if (gm.noArrows) flags |= 1 << (int) ModifierBit.NoArrows;
        if (gm.proMode) flags |= 1 << (int) ModifierBit.ProMode;
        if (gm.zenMode) flags |= 1 << (int) ModifierBit.ZenMode;
        if (gm.smallCubes) flags |= 1 << (int) ModifierBit.SmallCubes;

        flags |= 1 << (int) (gm.energyType == GameplayModifiers.EnergyType.Battery
            ? ModifierBit.EnergyType_Battery
            : ModifierBit.EnergyType_Bar);

        var obstacleBit = gm.enabledObstacleType switch {
            GameplayModifiers.EnabledObstacleType.FullHeightOnly => ModifierBit.Obstacles_FullHeightOnly,
            GameplayModifiers.EnabledObstacleType.NoObstacles => ModifierBit.Obstacles_NoObstacles,
            _ => ModifierBit.Obstacles_All
        };
        flags |= 1 << (int) obstacleBit;

        var speedBit = gm.songSpeed switch {
            GameplayModifiers.SongSpeed.Slower => ModifierBit.SongSpeed_Slower,
            GameplayModifiers.SongSpeed.Faster => ModifierBit.SongSpeed_Faster,
            GameplayModifiers.SongSpeed.SuperFast => ModifierBit.SongSpeed_SuperFast,
            _ => ModifierBit.SongSpeed_Normal
        };
        flags |= 1 << (int) speedBit;

        return flags;
    }

    private static async Task<byte[]> GetCoverPngAsync(BeatmapLevel level) {
        var sprite = await level.previewMediaData.GetCoverSpriteAsync()
            ?? throw new InvalidOperationException("Cover sprite is null.");

        var texture = ExtractReadableTexture(sprite);
        var png = texture.EncodeToPNG();

        Object.Destroy(texture);
        return png;
    }

    private static Texture2D ExtractReadableTexture(Sprite sprite) {
        var sourceTex = sprite.texture;
        var rect = sprite.textureRect;

        var tmp = RenderTexture.GetTemporary(
            sourceTex.width,
            sourceTex.height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Linear
        );

        Graphics.Blit(sourceTex, tmp);

        var previous = RenderTexture.active;
        RenderTexture.active = tmp;

        var readableTex = new Texture2D((int) rect.width, (int) rect.height, TextureFormat.RGBA32, false);
        readableTex.ReadPixels(new Rect(rect.x, rect.y, rect.width, rect.height), 0, 0);
        readableTex.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(tmp);

        return readableTex;
    }

    private static float? GetNoteJumpSpeed(BeatmapDifficulty difficulty, BeatmapBasicData data) {
        var njs = data.noteJumpMovementSpeed;
        if (njs > 0) return njs;

        return difficulty switch {
            BeatmapDifficulty.Easy or BeatmapDifficulty.Normal or BeatmapDifficulty.Hard => 10f,
            BeatmapDifficulty.Expert => 12f,
            BeatmapDifficulty.ExpertPlus => 16f,
            _ => null
        };
    }

    private static string ExtractDifficultyName(BeatmapLevel level, BeatmapKey key) {
        var difficultyName = key.difficulty.ToString("g");

        if (!level.levelID.StartsWith("custom_level_")) return difficultyName;

        var extraData = Collections.GetCustomLevelSongData(level.levelID);
        var customDiffData = extraData?._difficulties.FirstOrDefault(x =>
            x._difficulty == key.difficulty &&
            x._beatmapCharacteristicName == key.beatmapCharacteristic.serializedName);

        if (customDiffData is not null && !string.IsNullOrWhiteSpace(customDiffData._difficultyLabel)) {
            difficultyName = customDiffData._difficultyLabel;
        }

        return difficultyName;
    }

    public void Dispose() {
        pauseController.didPauseEvent -= HandlePaused;
        pauseController.didResumeEvent -= HandleResumed;
        pauseController.didReturnToMenuEvent -= HandleQuit;
        levelEndActions.levelFinishedEvent -= HandleFinished;
        levelEndActions.levelFailedEvent -= HandleFailed;
    }
}

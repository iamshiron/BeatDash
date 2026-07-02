using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shiron.BeatDash.Data.Socket;
using Shiron.BeatDash.Mod.Network;
using SiraUtil.Zenject;
using SongCore;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shiron.BeatDash.Mod.Trackers;

/// <summary>
/// Collects beatmap metadata and cover art at scene load, then transmits them over the socket.
/// </summary>
public sealed class LevelDataTracker(GameplayCoreSceneSetupData setupData, NetworkManager networkManager)
    : IAsyncInitializable, IDisposable {

    /// <summary>
    /// Builds the <see cref="MapStartMessage"/> from scene data and sends it along with the cover image.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct) {
        var level = setupData.beatmapLevel;
        var key = setupData.beatmapKey;
        var basicData = setupData.beatmapBasicData;
        var transformedData = setupData.transformedBeatmapData;
        var characteristic = key.beatmapCharacteristic;

        var coverPng = await GetCoverPngAsync(level);

        var correlationId = UnityEngine.Random.Range(0, int.MaxValue);

        var mapPayload = new MapStartMessage {
            CorrelationId = correlationId,
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
                Requires360Movement = characteristic.requires360Movement,
            },
        };

        var imageData = MapCoverImagePacket.Build(correlationId, coverPng);
        var imagePayload = new BinaryPacket(BinaryPacketTypes.MapCoverImage, imageData);

        Plugin.Log.Info($"Sending map data: {mapPayload.SongName} - {mapPayload.SongAuthor} ({imagePayload.Payload.Length} bytes) [corr={correlationId}]");

        await networkManager.PostMessageAsync(JsonConvert.SerializeObject(mapPayload));
        await networkManager.PostMessageAsync(imagePayload);
    }

    /// <summary>
    /// Encodes the level's cover image to PNG bytes.
    /// </summary>
    private static async Task<byte[]> GetCoverPngAsync(BeatmapLevel level) {
        var sprite = await level.previewMediaData.GetCoverSpriteAsync()
            ?? throw new InvalidOperationException("Cover sprite is null.");

        var texture = ExtractReadableTexture(sprite);
        var png = texture.EncodeToPNG();

        Object.Destroy(texture);
        return png;
    }

    /// <summary>
    /// Creates a CPU-readable copy of a sprite's texture via a render-to-texture blit.
    /// </summary>
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

        UnityEngine.Graphics.Blit(sourceTex, tmp);

        var previous = RenderTexture.active;
        RenderTexture.active = tmp;

        var readableTex = new Texture2D((int) rect.width, (int) rect.height, TextureFormat.RGBA32, false);
        readableTex.ReadPixels(new Rect(rect.x, rect.y, rect.width, rect.height), 0, 0);
        readableTex.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(tmp);

        return readableTex;
    }

    /// <summary>
    /// Returns the note-jump speed, falling back to difficulty-based defaults when unset.
    /// </summary>
    private static float? GetNoteJumpSpeed(BeatmapDifficulty difficulty, BeatmapBasicData data) {
        var njs = data.noteJumpMovementSpeed;
        if (njs > 0) return njs;

        return difficulty switch {
            BeatmapDifficulty.Easy or BeatmapDifficulty.Normal or BeatmapDifficulty.Hard => 10f,
            BeatmapDifficulty.Expert => 12f,
            BeatmapDifficulty.ExpertPlus => 16f,
            _ => null,
        };
    }

    /// <summary>
    /// Resolves the display difficulty name, preferring custom labels for custom levels.
    /// </summary>
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

    /// <inheritdoc/>
    public void Dispose() { }
}

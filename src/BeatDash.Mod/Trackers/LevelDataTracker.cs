using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BeatSaverSharp;
using Newtonsoft.Json;
using Shiron.BeatDash.Data.Socket;
using Shiron.BeatDash.Mod.Network;
using SiraUtil.Zenject;
using SongCore;
using Zenject;
using Unity;
using UnityEngine;
using WebSocketSharp;
using Graphics = System.Drawing.Graphics;
using Object = UnityEngine.Object;

namespace Shiron.BeatDash.Mod.Trackers;

public class LevelDataTracker(GameplayCoreSceneSetupData setupData, NetworkManager networkManager) : IAsyncInitializable, IDisposable {
    private static readonly BeatSaver beatSaver = new(Plugin.PluginName, Assembly.GetExecutingAssembly().GetName().Version);

    public async Task InitializeAsync(CancellationToken ct) {
        var level = setupData.beatmapLevel;
        var key = setupData.beatmapKey;
        var basicData = setupData.beatmapBasicData;
        var coverSprite = await level.previewMediaData.GetCoverSpriteAsync();
        var texture = coverSprite.texture;
        var (textureData, textureWidth, textureHeight, format) = await GetCoverBytesAsync(level);
        var transformedData = setupData.transformedBeatmapData;

        var characteristics = setupData.beatmapKey.beatmapCharacteristic;

        var mapPayload = new MapStartMessage {
            LevelId = level.levelID,
            DurationMs = (int) (level.songDuration * 1000f),
            NotesPerSecond = transformedData.cuttableNotesCount / level.songDuration,
            SongName = level.songName,
            SongSubName = level.songSubName,
            SongAuthor = level.songAuthorName,
            Mapper = level.allMappers.Length > 0
                ? string.Join(", ", level.allMappers)
                : level.songAuthorName
                ?? "Unknown",
            Bpm = level.beatsPerMinute,
            Difficulty = key.difficulty.SerializedName(),
            NoteJumpSpeed = GetNjs(key.difficulty, basicData),
            BombsCount = transformedData.bombsCount,
            CuttableObjectsCount = transformedData.cuttableNotesCount,
            ObstaclesCount = transformedData.obstaclesCount,
            LaneCount = transformedData.numberOfLines,

            Characteristic = new BeatmapCharacteristic {
                SerializedName = characteristics.serializedName,
                ContainsRotationEvents = characteristics.containsRotationEvents,
                DescriptionLocalizationKey = characteristics.descriptionLocalizationKey,
                LocalizationKey = characteristics.characteristicNameLocalizationKey,
                NumberOfColors = characteristics.numberOfColors,
                Requires360Movement = characteristics.requires360Movement
            }
        };

        var imagePayload = new BinaryPacket(BinaryPacketTypes.MapCoverImage, textureData);

        Plugin.Log.Info($"Sending map data: {mapPayload.SongName} - {mapPayload.SongAuthor} - {imagePayload.Payload.Length} bytes");
        var jsonPayload = JsonConvert.SerializeObject(mapPayload);
        Plugin.Log.Info($"Map JSON Payload: {jsonPayload}");

        await networkManager.PostMessageAsync(jsonPayload);
        await networkManager.PostMessageAsync(imagePayload);
    }

    private async Task<(byte[] data, int width, int height, TextureFormat format)> GetCoverBytesAsync(BeatmapLevel level) {
        var sprite = await level.previewMediaData.GetCoverSpriteAsync();
        if (sprite == null) {
            throw new InvalidOperationException("Cover sprite is null!");
        }

        var texture = ExtractReadableTexture(sprite);
        var data = texture.EncodeToPNG();

        var width = texture.width;
        var height = texture.height;
        var format = texture.format;

        Object.Destroy(texture);
        return (data, width, height, format);
    }

    private Texture2D ExtractReadableTexture(Sprite sprite) {
        var sourceTex = sprite.texture;
        var r = sprite.textureRect;

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

        var readableTex = new Texture2D((int) r.width, (int) r.height, TextureFormat.RGBA32, false);

        readableTex.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
        readableTex.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(tmp);

        return readableTex;
    }

    private static float? GetNjs(BeatmapDifficulty difficulty, BeatmapBasicData data) {
        var njs = data.noteJumpMovementSpeed;

        if (njs > 0) return njs;
        return difficulty switch {
            BeatmapDifficulty.Easy or BeatmapDifficulty.Normal or BeatmapDifficulty.Hard => 10f,
            BeatmapDifficulty.Expert => 12f,
            BeatmapDifficulty.ExpertPlus => 16f,
            _ => null
        };
    }

    public void Dispose() {
    }
}

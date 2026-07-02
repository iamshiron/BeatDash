using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BeatSaverSharp;
using Newtonsoft.Json;
using Shiron.BeatDash.Data.Socket;
using Shiron.BeatDash.Mod.Network;
using SiraUtil.Zenject;
using Zenject;
using Unity;
using UnityEngine;
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

        Plugin.Log.Debug($"Sprite: {coverSprite}");
        Plugin.Log.Debug($"Texture: {texture}");
        Plugin.Log.Debug($"Data Length: {textureData.Length}, {textureWidth}x{textureHeight}, Format: {format.ToString()}");

        var mapPayload = new MapStartMessage {
            SongName = level.songName,
            SongSubName = level.songSubName,
            SongAuthor = level.songAuthorName,
            Mapper = level.allMappers.Length > 0
                ? string.Join(", ", level.allMappers)
                : level.songAuthorName
                ?? "Unknown",
            BPM = level.beatsPerMinute,
            Difficulty = key.difficulty.ToString("g"),
            NJS = basicData.noteJumpMovementSpeed
        };

        var imagePayload = new BinaryPacket(BinaryPacketTypes.MapCoverImage, textureData);

        Plugin.Log.Info($"Sending map data: {mapPayload.SongName} - {mapPayload.SongAuthor} - {imagePayload.Payload.Length} bytes");
        await networkManager.PostMessageAsync(JsonConvert.SerializeObject(mapPayload));
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
        var r = sprite.textureRect; // The specific crop of the atlas for this song

        // Create a temporary RenderTexture with the full atlas dimensions
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

    public void Dispose() {
    }
}

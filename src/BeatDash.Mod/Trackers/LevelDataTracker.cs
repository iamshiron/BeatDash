using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shiron.BeatDash.Data.Socket;
using Shiron.BeatDash.Mod.Network;
using SiraUtil.Zenject;
using Zenject;

namespace Shiron.BeatDash.Mod.Trackers;

public class LevelDataTracker(GameplayCoreSceneSetupData setupData, NetworkManager networkManager) : IAsyncInitializable, IDisposable {
    public async Task InitializeAsync(CancellationToken ct) {
        var level = setupData.beatmapLevel;
        var key = setupData.beatmapKey;
        var basicData = setupData.beatmapBasicData;

        var payload = new MapStartMessage {
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

        var json = JsonConvert.SerializeObject(payload);
        Plugin.Log.Info($"Map Start: {json}");

        await networkManager.PostMessageAsync(json);
    }

    public void Dispose() {
    }
}

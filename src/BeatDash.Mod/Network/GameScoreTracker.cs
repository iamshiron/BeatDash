namespace Shiron.BeatDash.Mod.Network;

public class GameScoreTracker(NetworkManager networkManager) {
    public async void SendTestMessage() {
        await networkManager.PostMessageAsync("Test");
    }
}

using BeatSaberMarkupLanguage.Settings;
using BeatSaberMarkupLanguage.Util;
using IPA;
using IPA.Loader;
using Shiron.BeatDash.Mod.UI;
using IpaLogger = IPA.Logging.Logger;

namespace Shiron.BeatDash.Mod;

[Plugin(RuntimeOptions.DynamicInit)]
internal class Plugin {
    internal static IpaLogger Log { get; private set; } = null!;

    private readonly Settings _settings = new();

    [Init]
    public Plugin(IpaLogger ipaLogger, PluginMetadata pluginMetadata) {
        Log = ipaLogger;
        Log.Info($"{pluginMetadata.Name} {pluginMetadata.HVersion} initialized.");
    }

    [OnStart]
    public void OnApplicationStart() {
        Log.Debug("OnApplicationStart");

        MainMenuAwaiter.MainMenuInitializing += MainMenuInit;
    }

    private void MainMenuInit() {
        BSMLSettings.Instance.AddSettingsMenu(
            "BeatDash",
            "Shiron.BeatDash.Mod.UI.Settings.bsml",
            _settings
        );
    }

    [OnExit]
    public void OnApplicationQuit() {
        Log.Debug("OnApplicationQuit");
    }
}

using BeatSaberMarkupLanguage.Settings;
using BeatSaberMarkupLanguage.Util;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using IPA.Loader;
using Shiron.BeatDash.Mod.Installers;
using Shiron.BeatDash.Mod.UI;
using SiraUtil.Zenject;
using IpaLogger = IPA.Logging.Logger;

namespace Shiron.BeatDash.Mod;

[Plugin(RuntimeOptions.DynamicInit)]
internal class Plugin {
    internal static IpaLogger Log { get; private set; } = null!;

    private readonly SettingsViewController _settingsViewController = new();

    [Init]
    public Plugin(IpaLogger ipaLogger, Config config, PluginMetadata pluginMetadata, Zenjector zenjector) {
        Log = ipaLogger;
        Log.Info($"{pluginMetadata.Name} {pluginMetadata.HVersion} initialized.");

        var pluginConfig = config.Generated<PluginConfig>();
        zenjector.Install<AppInstaller>(Location.App, pluginConfig);
        zenjector.Install<MenuInstaller>(Location.Menu);
    }

    [OnStart]
    public void OnApplicationStart() {
        Log.Debug("OnApplicationStart");

        MainMenuAwaiter.MainMenuInitializing += MainMenuInit;
    }

    private void MainMenuInit() {
        // BSMLSettings.Instance.AddSettingsMenu(
        //     "BeatDash",
        //     "Shiron.BeatDash.Mod.UI.SettingsView.bsml",
        //     _settingsView
        // );

        Log.Info($"Host: {_settingsViewController.Host.Text}");
    }

    [OnExit]
    public void OnApplicationQuit() {
        Log.Debug("OnApplicationQuit");
    }
}

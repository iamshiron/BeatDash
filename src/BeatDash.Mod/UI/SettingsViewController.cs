using System.ComponentModel;
using System.Reflection;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Util;
using BeatSaberMarkupLanguage.ViewControllers;
using JetBrains.Annotations;
using TMPro;
using Zenject;

namespace Shiron.BeatDash.Mod.UI;

[HotReload(RelativePathToLayout = @".\SettingsView.bsml")]
[ViewDefinition("Shiron.BeatDash.Mod.UI.SettingsView.bsml")]
internal class SettingsViewController : BSMLAutomaticViewController {
    [Inject]
    private readonly PluginConfig _config = null!;

    [UIComponent("host")]
    [UsedImplicitly]
    public StringSetting Host = null!;

    public override void __Activate(bool addedToHierarchy, bool screenSystemEnabling) {
        base.__Activate(addedToHierarchy, screenSystemEnabling);

        Host.Text = _config.Host;
        Host.ApplyValue();
    }

    public override void __Deactivate(bool removedFromHierarchy, bool deactivateGameObject, bool screenSystemDisabling) {
        base.__Deactivate(removedFromHierarchy, deactivateGameObject, screenSystemDisabling);

        _config.Host = Host.Text;
        Plugin.Log.Info($"Saving Config!");
        Plugin.Log.Info($"Host: {_config.Host}");
    }
}

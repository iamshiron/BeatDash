using System.ComponentModel;
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
    [UIComponent("host")]
    [UsedImplicitly]
    public StringSetting Host = null!;
}

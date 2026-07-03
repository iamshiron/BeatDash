using System;
using System.ComponentModel;
using System.Net;
using System.Reflection;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Util;
using BeatSaberMarkupLanguage.ViewControllers;
using JetBrains.Annotations;
using TMPro;
using Zenject;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shiron.BeatDash.Mod.Network;
using UnityEngine.Serialization;

namespace Shiron.BeatDash.Mod.UI;

[HotReload(RelativePathToLayout = @".\SettingsView.bsml")]
[ViewDefinition("Shiron.BeatDash.Mod.UI.SettingsView.bsml")]
internal class SettingsViewController : BSMLAutomaticViewController {
    [Inject]
    private readonly PluginConfig _config = null!;
    [Inject]
    private readonly NetworkManager _networkManager = null!;

    [UIComponent("host")]
    [UsedImplicitly]
    public StringSetting Host = null!;

    [UIComponent("pin")]
    [UsedImplicitly]
    public StringSetting Pin = null!;

    [UIComponent("disableUdp")]
    [UsedImplicitly]
    public ToggleSetting DisableUdp = null!;

    [UIComponent("transmitBufferSize")]
    [UsedImplicitly]
    public SliderSetting TransmitBufferSize = null!;

    [UIComponent("disableDoubleBuffering")]
    [UsedImplicitly]
    public ToggleSetting DisableDoubleBuffering = null!;

    public override void __Activate(bool addedToHierarchy, bool screenSystemEnabling) {
        base.__Activate(addedToHierarchy, screenSystemEnabling);

        Host.Text = _config.Host;
        Host.ApplyValue();

        DisableUdp.Value = _config.DisableUdp;
        DisableUdp.ApplyValue();

        TransmitBufferSize.Value = _config.TransmissionBufferSize;
        TransmitBufferSize.ApplyValue();
    }

    public async Task OnSubmitPin() {
        try {
            using var httpClient = new HttpClient();
            var content = new StringContent(
                JsonConvert.SerializeObject(new DeviceAuthRequest(Pin.Text, _config.ClientId)),
                Encoding.UTF8,
                "application/json"
            );

            var response = await httpClient.PostAsync($"{(_config.UseSsl ? "https" : "http")}://{Host.Text}/api/device/authenticate", content);

            var tokenPair = JsonConvert.DeserializeObject<DeviceAuthResponse>(response.Content.ReadAsStringAsync().Result);
            if (tokenPair == null) throw new Exception("TokenPair is null!");

            Plugin.Log.Info($"Got TokenPair!");
            Plugin.Log.Info($"Refresh Token: {tokenPair.RefreshToken}");
            Plugin.Log.Info($"Access Token: {tokenPair.AccessToken}");

            _config.AccessToken = tokenPair.AccessToken;
            _config.RefreshToken = tokenPair.RefreshToken;
            await _networkManager.ConnectToSocketAsync();
        } catch (Exception e) {
            Plugin.Log.Error(e.ToString());
        }
    }

    public override void __Deactivate(bool removedFromHierarchy, bool deactivateGameObject, bool screenSystemDisabling) {
        base.__Deactivate(removedFromHierarchy, deactivateGameObject, screenSystemDisabling);

        _config.Host = Host.Text;
        _config.DisableUdp = DisableUdp.Value;
        _config.TransmissionBufferSize = (int) TransmitBufferSize.Value;
        _config.DisableDoubleBuffering = DisableDoubleBuffering.Value;
        Plugin.Log.Info($"Saving Config!");
        Plugin.Log.Info($"Host: {_config.Host}");
    }
}

public class DeviceAuthRequest(string pin, string clientId) {
    public string Pin { get; set; } = pin;
    public string ClientId { get; set; } = clientId;
}

public class DeviceAuthResponse {
    public DateTime RefreshExpiresAt { get; set; }
    public DateTime AccessExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
}

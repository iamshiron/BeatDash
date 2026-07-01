using System;
using HMUI;
using Zenject;

namespace Shiron.BeatDash.Mod.UI;

public class BeatDashFlowCoordinator : FlowCoordinator {
    [Inject]
    private readonly SettingsViewController _settingsViewController = null!;
    public event Action? DidFinish;

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling) {
        if (firstActivation) {
            showBackButton = true;
            SetTitle("BeatDash");
        }

        if (addedToHierarchy) {
            ProvideInitialViewControllers(_settingsViewController);
        }
    }

    protected override void BackButtonWasPressed(ViewController topViewController) {
        DidFinish?.Invoke();
    }
}

using System;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;
using JetBrains.Annotations;
using Zenject;

namespace Shiron.BeatDash.Mod.UI;

[UsedImplicitly]
public class MenuButtonManager : IInitializable, IDisposable {
    private readonly MainFlowCoordinator _mainFlowCoordinator;
    private readonly BeatDashFlowCoordinator _beatDashFlowCoordinator;
    private readonly MenuButtons _menuButtons;
    private readonly MenuButton _menuButton;

    public MenuButtonManager(MainFlowCoordinator mainFlowCoordinator, BeatDashFlowCoordinator beatDashFlowCoordinator, MenuButtons menuButtons) {
        _mainFlowCoordinator = mainFlowCoordinator;
        _beatDashFlowCoordinator = beatDashFlowCoordinator;
        _menuButtons = menuButtons;
        _menuButton = new MenuButton("BeatDash", PresentFlowCoordinator);
    }

    private void PresentFlowCoordinator() {
        _mainFlowCoordinator.PresentFlowCoordinator(_beatDashFlowCoordinator);
    }

    private void DismissFlowCoordinator() {
        _mainFlowCoordinator.DismissFlowCoordinator(_beatDashFlowCoordinator);
    }

    public void Initialize() {
        _menuButtons.RegisterButton(_menuButton);
        _beatDashFlowCoordinator.DidFinish += DismissFlowCoordinator;
    }
    public void Dispose() {
        _beatDashFlowCoordinator.DidFinish -= DismissFlowCoordinator;
    }
}

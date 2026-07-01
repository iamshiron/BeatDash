using Shiron.BeatDash.Mod.UI;
using Zenject;

namespace Shiron.BeatDash.Mod.Installers;

public class MenuInstaller : Installer {
    public override void InstallBindings() {
        Container.BindInterfacesTo<MenuButtonManager>().AsSingle();
        Container.Bind<BeatDashFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
        Container.Bind<SettingsViewController>().FromNewComponentAsViewController().AsSingle();
    }
}

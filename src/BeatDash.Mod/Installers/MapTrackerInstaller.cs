using JetBrains.Annotations;
using Shiron.BeatDash.Mod.Trackers;
using Zenject;

namespace Shiron.BeatDash.Mod.Installers;

[UsedImplicitly]
public class MapTrackerInstaller : Installer {
    public override void InstallBindings() {
        Container.BindInterfacesAndSelfTo<GameplaySessionTracker>().AsSingle();
    }
}

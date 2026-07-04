using JetBrains.Annotations;
using Shiron.BeatDash.Mod.Network;
using Shiron.BeatDash.Mod.Trackers;
using Zenject;

namespace Shiron.BeatDash.Mod.Installers;

[UsedImplicitly]
public class MapTrackerInstaller : Installer {
    public override void InstallBindings() {
        Container.Bind<GameplaySession>().AsSingle();
        Container.Bind<StatAccumulatorService>().AsSingle();
        Container.BindInterfacesAndSelfTo<GameplaySessionTracker>().AsSingle();
        Container.BindInterfacesAndSelfTo<LiveStatsTracker>().AsSingle();
    }
}

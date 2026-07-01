using JetBrains.Annotations;
using Zenject;

namespace Shiron.BeatDash.Mod.Installers;

[UsedImplicitly]
internal class AppInstaller(PluginConfig config) : Installer {
    public override void InstallBindings() {
        Container.BindInstance(config).AsSingle();
    }
}

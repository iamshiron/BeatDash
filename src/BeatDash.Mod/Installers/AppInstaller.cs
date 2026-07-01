using System.Net.Http;
using JetBrains.Annotations;
using Shiron.BeatDash.Mod.Network;
using Zenject;

namespace Shiron.BeatDash.Mod.Installers;

[UsedImplicitly]
internal class AppInstaller(PluginConfig config) : Installer {
    public override void InstallBindings() {
        Container.BindInstance(config).AsSingle();
        Container.Bind<TokenRefreshHandler>().AsTransient();
        Container.BindInterfacesAndSelfTo<NetworkManager>().AsSingle();

        Container.Bind<HttpClient>().FromMethod(context => {
            var handler = context.Container.Resolve<TokenRefreshHandler>();
            handler.InnerHandler = new HttpClientHandler();
            return new HttpClient(handler);
        }).AsSingle();

        Container.BindInterfacesTo<PluginStartup>().AsSingle();
    }
}

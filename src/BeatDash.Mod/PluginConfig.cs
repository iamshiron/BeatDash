using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using JetBrains.Annotations;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace Shiron.BeatDash.Mod;

[UsedImplicitly]
internal class PluginConfig {
    public virtual string Host { get; set; } = "http://192.168.1.19:1811";
}

using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace Shiron.BeatDash.Mod;

internal class PluginConfig {
    public virtual string Host { get; set; } = "http://192.168.1.19:1811";
}

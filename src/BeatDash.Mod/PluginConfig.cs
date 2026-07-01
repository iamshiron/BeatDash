using System;
using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using JetBrains.Annotations;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace Shiron.BeatDash.Mod;

[UsedImplicitly]
public class PluginConfig {
    public virtual string Host { get; set; } = "127.0.0.1:1811";
    public virtual bool UseSsl { get; set; } = false;

    public virtual string ClientId { get; set; } = Guid.NewGuid().ToString();
    public virtual string? AccessToken { get; set; } = null;
    public virtual string? RefreshToken { get; set; } = null;
}

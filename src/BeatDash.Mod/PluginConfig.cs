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

    /// <summary>
    /// When false (default), UDP is enabled and live traffic is sent over UDP.
    /// </summary>
    public virtual bool DisableUdp { get; set; } = false;

    public virtual int TransmissionBufferSize { get; set; } = 50;
    public virtual bool DisableDoubleBuffering { get; set; } = true;

    /// <summary>
    /// Saber/head motion capture rate in Hz. <c>0</c> captures every frame.
    /// The server decimates down to its own target rate.
    /// </summary>
    public virtual int MotionSampleRate { get; set; } = 0;

    /// <summary>
    /// Number of motion frames accumulated before the buffer is swapped and
    /// flushed as a <see cref="Shiron.BeatDash.Data.Socket.BinaryPacketTypes.MotionFrameBatch"/>.
    /// </summary>
    public virtual int MotionFrameBufferSize { get; set; } = 200;
}

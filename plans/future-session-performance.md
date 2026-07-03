# Future Telemetry: Session & Performance Metadata

Excluded from current scope. To be revisited later.

## Data Items

| Item | Source API | Notes |
|---|---|---|
| FPS / frame time | `Time.deltaTime`, `Time.unscaledDeltaTime` (UnityEngine) | Average over the batch window. Available globally, no injection needed. |
| WebSocket latency | Measure round-trip on `PostMessageAsync` in `NetworkManager` | Echo a timestamp in a ping message, measure delta on any response. Requires server-side echo support. |
| VR device type | `UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.Head)` → `.name` / `.manufacturer` | Available via `UnityEngine.XRModule`. May return empty string on desktop mode. |
| Mod version | `Plugin.PluginMetadata.HVersion` (IPA.Loader) | Already available via `IPA.Loader.PluginMetadata`. |
| Game version | `UnityEngine.Application.gameVersion` or build info | String like `1.40.8_7379`. |
| Platform | `UnityEngine.Application.platform` | Enum: `WindowsPlayer`, `WindowsEditor`, `Android`, etc. Identifies PC vs Quest. |

## Where to Find Each

- **FPS**: `UnityEngine.Time` — static properties, no DI needed. Read in `Tick()`.
- **Latency**: Would need a ping/pong protocol added to `NetworkManager` (`src/BeatDash.Mod/Network/NetworkManager.cs`).
- **VR device**: `UnityEngine.XR.InputDevices` — static API. Reference `UnityEngine.XRModule.dll`.
- **Mod/Game version**: `IPA.Loader.PluginMetadata` (injected in `Plugin.cs`), `UnityEngine.Application`.
- **Platform**: `UnityEngine.Application.platform` — static, no DI.

# BeatDash mod

This ZIP is the installable mod, NOT the server or GitHub's source-code archive.
It targets Beat Saber **1.40.8**. Use a matching modded game installation.

1. Close Beat Saber.
2. Install the mod's dependencies for your game version using your mod manager:
   BSIPA, SiraUtil, BSML, SongCore, SongDetailsCache, BeatSaverSharp and
   websocket-sharp (and their dependencies, including Harmony/Hive.Versioning).
3. Extract the ZIP into the Beat Saber install directory, merging `Plugins` and
   `Libs`. The mod DLL goes in `Plugins`; `Shiron.BeatDash.Data.dll` goes in `Libs`.
   Do NOT put the entire ZIP inside `Plugins`.
4. Start your BeatDash server and open its web dashboard. Create/log in to an
   account, request a device pairing PIN, and enter the server address and PIN
   in the BeatDash mod's settings.

Only BeatDash's own binaries are included. Proprietary game DLLs and third-party
mods must come from your licensed game installation and mod manager.

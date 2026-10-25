# BeatDash server

1. Install the **.NET 10 ASP.NET Core Runtime** for this ZIP's platform (Windows x64
   or Linux x64). The SDK, Node.js and pnpm are NOT needed. On Windows choose the
   ASP.NET Core Runtime, not just the Desktop Runtime. Downloads:
   https://dotnet.microsoft.com/download/dotnet/10.0
2. Extract the ENTIRE ZIP to a persistent, writable directory. Do not run inside
   the ZIP or copy only the executable. Keep `wwwroot` and all DLLs beside it.
3. Provision PostgreSQL 17 and RustFS (S3 port 9000).
   Create a `beatdash` database and a user that can apply schema migrations.
4. Create RustFS buckets `beatdash-assets` (anonymous GetObject allowed) and
   `beatdash-user-data` (private). Configure durable volumes for BOTH services.
   Match the names, credentials and endpoints in the sample configuration.
5. Copy `appsettings.example.json` to **`appsettings.json`** beside the server DLL.
   Read its comments and replace ALL `CHANGE_ME` values, the admin email/password,
   and the JWT secret. The file contains secrets: restrict file permissions and
   never put it inside `wwwroot`. The sample disables public signup by default.
6. Open a terminal in the extracted directory and run:

   Windows: `dotnet .\Shiron.BeatDash.API.dll`

   Linux: `dotnet ./Shiron.BeatDash.API.dll`

   Or run the platform executable (`Shiron.BeatDash.API.exe` on Windows;
   `chmod +x Shiron.BeatDash.API && ./Shiron.BeatDash.API` on Linux).
7. Open **http://localhost:1811** and log in with the administrator credentials.
   The first start applies migrations when `Database:ApplyMigrationsOnStartup`
   is true. Stop other instances and back up the database before upgrading.

Linux requires glibc (not Alpine/musl), libfontconfig and the normal .NET runtime
OS dependencies. On Ubuntu install `libfontconfig1` for native image processing.

## Configuration

The existing .NET configuration system is used, not YAML. `appsettings.json` is
loaded relative to the server binaries, even when launched from another directory.
Environment variables and command-line arguments override it. For example:
`Registration__DisableSignup=true` or `--Registration:DisableSignup=true`.
Existing `.env` files are supported from the working directory and its parents.
Restart after changing settings. Editing initial admin credentials does not change
an already-created account; use its password-change flow.

`Registration:DisableSignup=true` removes the ENTIRE `/api/auth/register` endpoint
from routing. Direct requests cannot create accounts; hiding the frontend is only
a convenience. Set it to false and restart to allow public signup. Existing users
can still log in; the administrator is created independently during startup.

For access from another machine, set `Urls` to `http://0.0.0.0:1811` and allow TCP
1811 and UDP 1811 through the firewall. For internet access, use a TLS reverse
proxy and strong credentials. `Hsp:PublicIngestUrl` must be the complete public
URL ending in `/api/hsp/ingest` when the proxy changes the address.

## Administration and upgrades

The admin CLI is in `cli/`: `dotnet cli/beatdash.dll --help` (use backslashes on
Windows). It talks to the running server for backups; the server's configuration
does not need to be copied into the CLI directory. Follow `BACKUPS.md` for backup
and recovery, including maintenance mode requirements.

Keep your `appsettings.json`, database, RustFS volumes and private backup workspace
when replacing binaries. Extract new releases into a new directory, copy your
configuration, stop the old instance, back up, then start the new one. Do not
overwrite a live installation or expose the backup workspace through the web root.

Report startup failures with the console log, but redact passwords and JWT keys.

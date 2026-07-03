# BeatDash

Beat Saber profile and analytics dashboard. A Beat Saber mod streams gameplay data to a web dashboard (React) backed by an ASP.NET Core API and PostgreSQL.

> **⚠️ Heavy Development Notice**
>
> BeatDash is in early development. Features are incomplete, the UI is rough, things will break, and the auth flow is not production-hardened. Expect breaking changes at any time. Do not rely on this for anything yet.

---

## For Players

BeatDash pairs your VR headset with a web dashboard so you can track the maps you play. When you start a song in-game, the mod sends map metadata (song, mapper, BPM, difficulty, cover art) to your dashboard in real time.

### Current Features

-   **Device pairing** — link one or more Beat Saber installs to your account via a PIN code
-   **Live map tracking** — maps you play appear on your dashboard instantly as you start them
-   **Multi-device support** — pair multiple headsets under one account
-   **Beatmap catalog** — browse submitted maps with difficulties and cover art

### How It Works

#### Auth & Pairing Flow

```
1. Register an account on the web dashboard (email + password)
2. Log in
3. Request a 6-digit PIN from the dashboard
4. Enter the PIN in the Beat Saber mod settings
5. The mod exchanges the PIN for tokens and stays connected
```

The mod authenticates over a WebSocket connection to the API. Access tokens expire every 15 minutes and refresh automatically — you only need to pair once per device.

### Setup

The backend runs in Docker. With Docker installed:

1.  Download the latest BeatDash mod release and place it in your Beat Saber `Plugins` folder
2.  Start the backend and dashboard:
    ```sh
    docker compose up -d
    ```
3.  Open the dashboard in your browser, register, and follow the pairing flow above

---

## For Developers

### Structure

```
apps/
  beatdash-web/        # Web client (React + Vite + TanStack Router/Query)
packages/
  ui/                  # @shiron/ui - shared component library (Tailwind, shadcn) [submodule]
backend/
  src/
    BeatDash.API/      # ASP.NET Core API (Minimal APIs, EF Core, SignalR, WebSocket)
    BeatDash.CLI/      # CLI tool
    BeatDash.Data/     # Data layer (EF Core entities + DbContext)
    BeatDash.BSIPA/    # Beat Saber mod (BSIPA plugin, Zenject, Harmony)
external/
  lib/                 # Shared native/external library [submodule]
docker/                # Docker Compose configs for local infrastructure
```

### Tech Stack

**Frontend:** React 19, Vite, TypeScript, TanStack Router/Query, Tailwind CSS v4, shadcn/ui, Orval, Biome

**Backend:** .NET 10, ASP.NET Core Minimal APIs, EF Core, PostgreSQL (Npgsql), MinIO, SignalR, JWT Bearer auth

**Mod:** BSIPA plugin framework, Zenject DI, Harmony patching, WebSocket client

### Architecture

-   **Beat Saber mod → API:** WebSocket (`/api/client/game`) with mixed JSON (metadata) and binary (cover art) messages
-   **Web client → API:** REST (Orval-generated client) + SignalR (`/client/web`) for real-time updates
-   **Auth:** ASP.NET Core Identity + JWT. Access tokens (15 min) + refresh tokens (30 days), per-device
-   **Storage:** PostgreSQL for relational data, MinIO for cover images

### Prerequisites

-   [.NET 10 SDK](https://dotnet.microsoft.com/)
-   [Node.js](https://nodejs.org/) + [pnpm](https://pnpm.io/)
-   [Docker](https://www.docker.com/)

### Getting Started

1.  Clone with submodules:

    ```sh
    git clone --recurse-submodules https://github.com/iamshiron/BeatDash.git
    ```

2.  Install dependencies and set up env:

    ```sh
    pnpm install
    cp .env.example .env
    ```

3.  Start infrastructure (PostgreSQL, MinIO, Adminer):

    ```sh
    docker compose up -d
    ```

4.  Run everything in dev mode:

    ```sh
    pnpm dev
    ```

### Common Commands

| Command                      | Description                                        |
| ---------------------------- | -------------------------------------------------- |
| `pnpm dev`                   | Start the web app and API in dev mode              |
| `pnpm build`                 | Build all projects                                 |
| `pnpm lint`                  | Lint all projects                                  |
| `pnpm format`                | Format all projects                                |
| `pnpm migrate`               | Apply EF Core database migrations                  |
| `pnpm nx <target> <project>` | Run a specific target on a specific Nx project     |

**.NET:**

```sh
dotnet build Shiron.BeatDash.slnx
dotnet test Shiron.BeatDash.slnx
dotnet run --project backend/src/BeatDash.API
```

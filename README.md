# BeatDash

Beat Saber profile and analytics dashboard. A React frontend (`apps/beatdash-web`) backed by an ASP.NET Core API (`backend/src/BeatDash.API`), backed by PostgreSQL.

Managed with [Nx](https://nx.dev), pnpm, and a .NET solution file (`Shiron.BeatDash.slnx`).

## Structure

```
apps/
  beatdash-web/        # BeatDash web client (React + Vite + TanStack Router/Query)
packages/
  ui/                  # @shiron/ui - shared React component library (Tailwind, Radix, shadcn-based) [submodule]
backend/
  src/
    BeatDash.API/      # BeatDash API (ASP.NET Core, EF Core, PostgreSQL)
    BeatDash.CLI/      # BeatDash CLI tool
    BeatDash.Data/     # BeatDash data layer
external/
  lib/                 # Shared native/external library [submodule]
docker/                # Docker Compose configs for local infrastructure
```

## Prerequisites

-   [.NET 10 SDK](https://dotnet.microsoft.com/)
-   [Node.js](https://nodejs.org/) (version managed via project config)
-   [pnpm](https://pnpm.io/)
-   [Docker](https://www.docker.com/) (for local infrastructure)

## Getting Started

1. Clone with submodules:

    ```sh
    git clone --recurse-submodules https://github.com/iamshiron/BeatDash.git
    ```

2. Install dependencies:

    ```sh
    pnpm install
    ```

3. Copy `.env.example` to `.env` and fill in the values:

    ```sh
    cp .env.example .env
    ```

4. Start infrastructure (PostgreSQL, Adminer) via Docker:

    ```sh
    docker compose up -d
    ```

5. Run all projects in dev mode:
    ```sh
    pnpm dev
    ```

## Common Commands

All commands are run from the repository root.

| Command                      | Description                                                 |
|------------------------------|-------------------------------------------------------------|
| `pnpm dev`                   | Start the web app and backend API in dev mode               |
| `pnpm build`                 | Build all projects                                          |
| `pnpm lint`                  | Lint all projects                                           |
| `pnpm format`                | Format all projects                                         |
| `pnpm migrate`               | Migrate the database based on the existing migration files  |
| `pnpm nx <target> <project>` | Run a specific target on a specific Nx project              |

### .NET

```sh
dotnet build Shiron.BeatDash.slnx
dotnet test Shiron.BeatDash.slnx
dotnet run --project backend/src/BeatDash.API
```

## Tooling

-   **Nx** - task orchestration, dependency graph, caching
-   **pnpm** - JavaScript/TypeScript package management with workspaces
-   **Biome** - linting and formatting for frontend code
-   **Vite** - frontend build tool
-   **TypeScript** - type checking
-   **Tailwind CSS v4** - utility-first CSS
-   **Orval** - OpenAPI client generation from backend specs
-   **Docker Compose** - local infrastructure (PostgreSQL, Adminer)
-   **Central Package Management** - .NET NuGet versions managed via `Directory.Packages.props`

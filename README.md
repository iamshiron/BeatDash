# Shiron Monorepo

My personal monorepo for side projects, all living under the `@shiron` scope. Managed with [Nx](https://nx.dev), pnpm, and a .NET solution file (`Shiron.slnx`).

## Structure

```
apps/                  # Frontend applications
  archive-web/         # Archive web client (React + Vite + TanStack Router/Query)
  beatdash-web/        # BeatDash web client (React + Vite + TanStack Router/Query)
  honami-git-web/      # HonamiGit web client (React + Vite + TanStack Router/Query)
  honami-system-web/   # HonamiSystem web client (React + Vite + TanStack Router/Query)
  mutils-web/          # Mutils web client (React + Vite + TanStack Router/Query)
  resonance-system-web/ # ResonanceSystem web client (React + Vite + TanStack Router/Query)
packages/              # Shared packages
  ui/                  # @shiron/ui - shared React component library (Tailwind, Radix, shadcn-based)
backend/               # .NET 10 backend services
  src/
    Archive.API/       # Archive API (ASP.NET Core, EF Core, PostgreSQL)
    Archive.DB/        # Archive database layer
    BeatDash.API/      # BeatDash API (ASP.NET Core, EF Core, PostgreSQL, MinIO)
    BeatDash.CLI/      # BeatDash CLI tool
    BeatDash.Data/     # BeatDash data layer
    HonamiGit.API/     # HonamiGit API (ASP.NET Core, EF Core, PostgreSQL)
    HonamiGit.CLI/     # HonamiGit CLI tool
    HonamiGit.DB/      # HonamiGit database layer
    HonamiSystem/      # HonamiSystem (nested project structure)
      DB/              # HonamiSystem database layer (EF Core, PostgreSQL, pgvector)
      Plugins/         # HonamiSystem plugin projects
        ExamplePlugin/ # Example plugin implementation
      SDK/             # HonamiSystem plugin SDK
      Server/          # HonamiSystem API (ASP.NET Core)
      Services/        # HonamiSystem service layer
    Lib.Types/         # Shared type library
    Lib.Types.EFCore/  # EF Core type extensions
    Lib.Types.Extension/ # Type extension utilities
    Mutils.API/        # Mutils API (ASP.NET Core, EF Core, PostgreSQL, Discord OAuth)
    Mutils.DB/         # Mutils database layer
    ResonanceSystem.API/     # ResonanceSystem API (ASP.NET Core, EF Core, PostgreSQL, MinIO)
    ResonanceSystem.Core/    # ResonanceSystem core/domain layer
    ResonanceSystem.DB/      # ResonanceSystem database layer (EF Core, PostgreSQL, ASP.NET Identity)
    ResonanceSystem.Services/ # ResonanceSystem service layer (MinIO, Tesseract OCR)
  tests/               # .NET test projects
docker/                # Docker Compose configs for local infrastructure
```

## Projects

### Archive

Web app with a React frontend (`apps/archive-web`) and an ASP.NET Core API backend (`backend/src/Archive.API`). Backed by PostgreSQL.

### BeatDash

Web app with a React frontend (`apps/beatdash-web`) and an ASP.NET Core API backend (`backend/src/BeatDash.API`). Backed by PostgreSQL and MinIO for object storage.

### HonamiGit

Web app with a React frontend (`apps/honami-git-web`) and an ASP.NET Core API backend (`backend/src/HonamiGit.API`). Includes a CLI tool (`backend/src/HonamiGit.CLI`). Backed by PostgreSQL.

### HonamiSystem

Web app with a React frontend (`apps/honami-system-web`) and an ASP.NET Core API backend (`backend/src/HonamiSystem/Server`). Uses a nested project structure with separate layers for database (`DB`), services (`Services`), plugin SDK (`SDK`), and extensible plugins (`Plugins`). Backed by PostgreSQL with pgvector support.

### Mutils

React web client (`apps/mutils-web`) with an ASP.NET Core API backend (`backend/src/Mutils.API`). Auth via Discord OAuth + JWT. PostgreSQL for persistence, MinIO for object storage.

### ResonanceSystem

Web app with a React frontend (`apps/resonance-system-web`) and an ASP.NET Core API backend (`backend/src/ResonanceSystem.API`). Uses a layered architecture with separate projects for core/domain (`ResonanceSystem.Core`), database (`ResonanceSystem.DB` with ASP.NET Identity + EF Core), and services (`ResonanceSystem.Services` with MinIO and Tesseract OCR). Backed by PostgreSQL and MinIO.

### Shared UI (`@shiron/ui`)

My shared React component library, used across all web apps. Tailwind CSS, Radix UI, shadcn.

## Prerequisites

-   [.NET 10 SDK](https://dotnet.microsoft.com/)
-   [Node.js](https://nodejs.org/) (version managed via project config)
-   [pnpm](https://pnpm.io/)
-   [Docker](https://www.docker.com/) (for local infrastructure)

## Getting Started

1. Install dependencies:

    ```sh
    pnpm install
    ```

2. Copy `.env.example` to `.env` and fill in the values:

    ```sh
    cp .env.example .env
    ```

3. Start infrastructure (PostgreSQL, Adminer) via Docker:

    ```sh
    docker compose up -d
    ```

4. Run all projects in dev mode:
    ```sh
    pnpm dev
    ```

## Common Commands

All commands are run from the repository root.

| Command                      | Description                                                 |
|------------------------------|-------------------------------------------------------------|
| `pnpm dev`                   | Start all apps and backend services in dev mode             |
| `pnpm build`                 | Build all projects                                          |
| `pnpm lint`                  | Lint all projects                                           |
| `pnpm format`                | Format all projects                                         |
| `pnpm migrate`               | Migrate all databases based on the existing migration files |
| `pnpm nx <target> <project>` | Run a specific target on a specific Nx project              |

### .NET

```sh
dotnet build Shiron.slnx
dotnet test Shiron.slnx
dotnet run --project backend/src/Mutils.API
dotnet run --project backend/src/BeatDash.API
dotnet run --project backend/src/HonamiSystem/Server
dotnet run --project backend/src/ResonanceSystem.API
```

## Tooling

-   **Nx** - task orchestration, dependency graph, caching
-   **pnpm** - JavaScript/TypeScript package management with workspaces
-   **Biome** - linting and formatting for frontend code
-   **Vite** - frontend build tool
-   **Vitest** - frontend unit tests
-   **TypeScript** - type checking
-   **Tailwind CSS v4** - utility-first CSS
-   **Orval** - OpenAPI client generation from backend specs
-   **Docker Compose** - local infrastructure (PostgreSQL, MinIO, Adminer)
-   **Central Package Management** - .NET NuGet versions managed via `Directory.Packages.props`

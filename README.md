# Shiron Monorepo

[![CI](https://github.com/iamshiron/monorepo/actions/workflows/ci.yml/badge.svg)](https://github.com/iamshiron/monorepo/actions/workflows/ci.yml)

My personal monorepo for side projects, all living under the `@shiron` scope. Managed with [Nx](https://nx.dev), pnpm, and a .NET solution file (`Shiron.slnx`).

## Structure
    
```
apps/                  # Frontend applications
  beatdash-web/        # BeatDash web client (React + Vite + TanStack Router/Query)
  mutils-web/          # Mutils web client (React + Vite + TanStack Router/Query)
packages/              # Shared packages
  ui/                  # @shiron/ui - shared React component library (Tailwind, Radix, shadcn-based)
backend/               # .NET 10 backend services
  src/
    BeatDash.API/      # BeatDash API (ASP.NET Core, EF Core, PostgreSQL, MinIO)
    Mutils.Api/        # Mutils API (ASP.NET Core, EF Core, PostgreSQL, Discord OAuth)
    Mutils.Core/       # Mutils domain layer
    Mutils.Infrastructure/ # Mutils data access / infrastructure
  tests/               # .NET test projects
docker/                # Docker Compose configs for local infrastructure
```

## Projects

### BeatDash

Web app with a React frontend (`apps/beatdash-web`) and an ASP.NET Core API backend (`backend/src/BeatDash.API`). Backed by PostgreSQL.

### Mutils

React web client (`apps/mutils-web`) with an ASP.NET Core API backend (`backend/src/Mutils.Api`). Auth via Discord OAuth + JWT. PostgreSQL for persistence, MinIO for object storage. Backend uses a clean architecture split across `Mutils.Core` and `Mutils.Infrastructure`.

### Shared UI (`@shiron/ui`)

My shared React component library, used across both web apps. Tailwind CSS, Radix UI, shadcn.

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

| Command                     | Description                                     |
| --------------------------- | ----------------------------------------------- |
| `pnpm dev`                  | Start all apps and backend services in dev mode |
| `pnpm build`                | Build all projects                              |
| `pnpm lint`                 | Lint all projects                               |
| `pnpm format`               | Format all projects                             |
| `npx nx <target> <project>` | Run a specific target on a specific Nx project  |

### .NET

```sh
dotnet build Shiron.slnx
dotnet test Shiron.slnx
dotnet run --project backend/src/Mutils.Api
dotnet run --project backend/src/BeatDash.API
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

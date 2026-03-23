# Mutils

> **Disclaimer:** This project is not affiliated with, endorsed by, or connected to Mudae in any way. Mutils is an independent fan project created for personal use and the community.

A toolkit for managing Mudae collections and tracking kakera earnings.

## Features

### Collection Management

Import your Mudae collection using `$mmy+k=rlz+dx+i-s` command output. Features include:

- Search and filter by name, series, key count, kakera value
- Sort by rank, name, kakera, claims, or keys
- Mark characters as enabled/disabled
- Track key counts with automatic type detection (Bronze 1-2, Silver 3-5, Gold 6-9, Chaos 10+)
- Add notes to characters
- Export to JSON with configurable filters

### Roll Calculator

Calculate wish and starwish probabilities based on server configuration:

- Pool settings: total characters, disabled limit, antidisabled count
- Badge bonuses: Silver Badge (up to +100%), Ruby Badge II+ (+50%)
- Kakera Tower perks: Perk 2 (starwish %), Perk 3 (reduces pool), Perk 4 (double key chance)
- $bw roll investment with diminishing returns calculator
- Optimizer to find ideal roll/boostwish split
- Save and load configurations

### Kakera Statistics

Track and analyze kakera income:

- Manual claim logging or bulk import from Discord logs
- Time range filtering (week, month, 3 months, year, all time)
- Charts: daily income, type distribution, cumulative growth, day-of-week activity
- Statistics: total, average per claim, daily average, median, standard deviation
- Rolling 7-day averages and trend analysis
- Linear regression projections
- Threshold calculator for target kakera goals

### Optimizer

Get suggestions for which characters to enable or disable based on:
- Kakera value thresholds
- Key type groupings
- Collection composition analysis

## Tech Stack

| Layer | Technology |
|-------|------------|
| Frontend | React 19, TypeScript, TanStack Router, TanStack Query, Tailwind CSS 4, Vite 6 |
| Backend | .NET 10, ASP.NET Core, Entity Framework Core |
| Database | PostgreSQL 17 |
| Storage | MinIO (S3-compatible) |
| Auth | Discord OAuth 2.0, JWT |
| Charts | Recharts |

## Project Structure

```
Mutils/
├── frontend/           # React + TypeScript frontend
│   └── src/
│       ├── routes/     # TanStack Router page components
│       ├── components/ # UI components (shadcn-based)
│       ├── lib/        # API client, utilities
│       └── hooks/      # Custom React hooks
├── backend/
│   └── src/
│       ├── Mutils.Api/           # ASP.NET Core endpoints
│       ├── Mutils.Core/          # Entities, DTOs, interfaces
│       └── Mutils.Infrastructure/ # Services, data access
├── docker/             # Docker Compose configuration
└── .env.example        # Environment template
```

## Getting Started

### Prerequisites

- Node.js 20+
- pnpm 9+
- .NET 10 SDK
- Docker (for PostgreSQL and MinIO)

### Setup

1. Clone the repository

2. Copy environment file:
   ```bash
   cp .env.example .env
   ```

3. Configure Discord OAuth:
   - Create a Discord application at https://discord.com/developers/applications
   - Set redirect URI to `http://localhost:5173/auth/callback`
   - Update `.env` with your client ID and secret

4. Start infrastructure:
   ```bash
   cd docker && docker-compose up -d
   ```

5. Run database migrations:
   ```bash
   cd backend/src/Mutils.Api
   dotnet ef database update
   ```

6. Start the backend:
   ```bash
   cd backend/src/Mutils.Api
   dotnet run
   ```

7. Start the frontend:
   ```bash
   cd frontend
   pnpm install
   pnpm dev
   ```

8. Open http://localhost:5173

### Development Commands

```bash
# Root level
pnpm dev          # Start all services
pnpm build        # Build all projects
pnpm lint         # Lint all projects
pnpm test         # Run all tests

# Frontend only
cd frontend
pnpm dev          # Start dev server
pnpm build        # Production build
pnpm typecheck    # TypeScript check

# Backend only
cd backend/src/Mutils.Api
dotnet run        # Start API server
dotnet test       # Run tests
```

## API Documentation

API documentation is available at `/scalar/v1` when running the backend.

## License

MIT

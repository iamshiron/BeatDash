# Screenshots

Regenerates the README images in `assets/` from a committed fixture. Each run starts a disposable
PostgreSQL + RustFS stack, seeds it from `fixture/`, runs the real API and web client against it and
captures the pages with Playwright. The development stack is never touched.

## Requirements

-   .NET 10 SDK with `dotnet-ef`, Node.js + pnpm
-   Docker or Podman with `compose`
-   Chromium for Playwright: `pnpm --filter @shiron/beatdash-screenshots exec playwright install chromium`

## Commands

Run from the repository root:

| Command                          | Description                                                       |
| -------------------------------- | ----------------------------------------------------------------- |
| `pnpm screenshots`               | Capture every screenshot                                          |
| `pnpm screenshots <name>...`     | Capture only the named screenshots, e.g. `pnpm screenshots maps`  |
| `pnpm screenshots list`          | List the screenshots and the files they write                     |
| `pnpm screenshots serve`         | Seed the fixture and keep the app running to browse it            |
| `pnpm screenshots export`        | Rebuild the fixture from the development stack                    |
| `pnpm screenshots down`          | Remove leftover fixture containers (e.g. after Ctrl+C or `--keep`) |

`--no-build` reuses the last Release build of the API and web client, `--keep` leaves the containers running.

## Adding a screenshot

Add an entry to `src/shots.ts`:

```ts
{
    name: "devices",
    output: "feature-devices.png",
    signedIn: true,
    path: "/devices",
},
```

`pnpm screenshots devices` then writes `assets/feature-devices.png`. Before capturing, the tool waits for
network requests, loading skeletons, fonts and images to settle. Routes that need an id can resolve it
through the API (see `play-detail`), and `prepare` can interact with the page first (open a tab, scroll).
Use `pnpm screenshots serve` to explore the fixture in a browser while writing the entry.

## Determinism

Captures are pixel-identical between runs. Seeding moves all fixture timestamps forward by whole weeks, so
the newest play always lands in the last week and weekday-based charts keep their shape. The browser clock is
fixed to that moment. The viewport (1440×900), theme, locale, time zone and reduced motion are fixed too.

## The fixture

`fixture/` holds a data-only dump of a curated database, the covers and profile images it references, and
`fixture.json` with the EF Core migration the dump was taken at and the fixture login. Seeding migrates an
empty database up to that migration, loads the dump and then applies every newer migration, so schema
changes don't invalidate the fixture.

`pnpm screenshots export` rebuilds it from the stack configured in the repository's `.env` (falling back to
`.env.example`). The source database is only dumped. `sql/curate.sql` then trims a copy down to the account
with the most plays and replaces its credentials and body metrics. It also drops tokens, heart-rate samples
and health clients, and keeps the played, liked and listed maps plus the newest 60 of the catalog. Review the
result before committing it.

Ports default to 55432 (Postgres), 59000 (RustFS) and 18110 (API). Override them with
`SCREENSHOTS_POSTGRES_PORT`, `SCREENSHOTS_STORAGE_PORT` and `SCREENSHOTS_API_PORT`, and set
`SCREENSHOTS_CONTAINER_CLI` to force `docker` or `podman`.

import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

const toolRoot = resolve(import.meta.dirname, "..");
const repoRoot = resolve(toolRoot, "../..");
const fixtureRoot = join(toolRoot, "fixture");

function port(name: string, fallback: number): number {
	const value = process.env[name];
	return value ? Number.parseInt(value, 10) : fallback;
}

export const paths = {
	repoRoot,
	toolRoot,
	compose: join(toolRoot, "docker-compose.yml"),
	curateSql: join(toolRoot, "sql", "curate.sql"),
	shiftTimeSql: join(toolRoot, "sql", "shift-time.sql"),
	manifest: join(fixtureRoot, "fixture.json"),
	databaseDump: join(fixtureRoot, "database.sql.gz"),
	storage: join(fixtureRoot, "storage"),
	output: join(repoRoot, "assets"),
	envExample: join(repoRoot, ".env.example"),
	devEnv: join(repoRoot, ".env"),
	dbProject: join(repoRoot, "src", "BeatDash.DB"),
	apiProject: join(repoRoot, "src", "BeatDash.API"),
	apiAssembly: join(
		repoRoot,
		"src",
		"BeatDash.API",
		"bin",
		"Release",
		"net10.0",
		"Shiron.BeatDash.API.dll",
	),
	// The API loads the first .env it finds walking up from its working directory, which
	// would point it at the development database. Running it from here prevents that.
	apiWorkDir: join(tmpdir(), "beatdash-screenshots"),
} as const;

export const ports = {
	postgres: port("SCREENSHOTS_POSTGRES_PORT", 55432),
	storage: port("SCREENSHOTS_STORAGE_PORT", 59000),
	api: port("SCREENSHOTS_API_PORT", 18110),
} as const;

/** Credentials of the disposable stack, mirrored in docker-compose.yml. */
export const stack = {
	postgresUser: "screenshots",
	postgresPassword: "screenshots",
	database: "beatdash",
	stagingDatabase: "staging",
	storageAccessKey: "screenshots",
	storageSecretKey: "screenshots",
	bucketAssets: "beatdash-assets",
	bucketUserData: "beatdash-user-data",
} as const;

export const images = {
	postgres: "docker.io/library/postgres:17",
	mc: "docker.io/minio/mc:latest",
} as const;

export const apiUrl = `http://127.0.0.1:${ports.api}`;

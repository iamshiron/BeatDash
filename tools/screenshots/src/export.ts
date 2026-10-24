import { createReadStream, createWriteStream, existsSync } from "node:fs";
import {
	copyFile,
	mkdir,
	mkdtemp,
	readFile,
	rm,
	writeFile,
} from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { pipeline } from "node:stream/promises";
import { parseEnv } from "node:util";
import { createGzip } from "node:zlib";
import sharp from "sharp";
import { paths, ports, stack } from "./config.ts";
import { dbExec, psql, query, shellQuote, tool } from "./containers.ts";
import {
	type FixtureManifest,
	hashIdentityPassword,
	storageFolders,
	writeManifest,
} from "./fixture.ts";
import { log } from "./process.ts";

const login = {
	email: "demo@beatdash.local",
	password: "screenshots",
} as const;

/** What the fixture keeps beyond the plays themselves. */
const curation = {
	catalogSize: 60,
	motionSessions: 3,
	coverSize: 256,
	/** Gap between the newest play and the moment the screenshots pretend to be taken. */
	referenceOffsetMs: 12 * 60 * 60 * 1000,
} as const;

interface DevelopmentStack {
	readonly databaseUrl: string;
	readonly storageUrl: string;
	readonly accessKey: string;
	readonly secretKey: string;
	readonly buckets: Readonly<Record<keyof typeof storageFolders, string>>;
}

/**
 * Rebuilds the committed fixture from the development stack described by the repository's
 * .env (falling back to .env.example). The source is only read; curation happens on a copy.
 */
export async function exportFixture(): Promise<void> {
	const source = await developmentStack();
	await copyIntoStaging(source.databaseUrl);

	log("Curating staging copy");
	await psql(
		stack.stagingDatabase,
		[
			"-v",
			`password_hash=${hashIdentityPassword(login.password)}`,
			"-v",
			`login_email=${login.email}`,
			"-v",
			`catalog_size=${curation.catalogSize}`,
			"-v",
			`motion_sessions=${curation.motionSessions}`,
		],
		{ input: createReadStream(paths.curateSql) },
	);

	await dumpStaging();
	await exportStorage(source);

	const manifest: FixtureManifest = {
		migration: await query(
			stack.stagingDatabase,
			`SELECT max("MigrationId") FROM "__EFMigrationsHistory"`,
		),
		referenceTime: await referenceTime(),
		login,
	};
	await writeManifest(manifest);
	await psql("postgres", [
		"-c",
		`DROP DATABASE ${stack.stagingDatabase} WITH (FORCE)`,
	]);
	log(`Fixture written at migration ${manifest.migration}`);
}

async function developmentStack(): Promise<DevelopmentStack> {
	const file = existsSync(paths.devEnv) ? paths.devEnv : paths.envExample;
	const env: Record<string, string | undefined> = {
		...parseEnv(await readFile(file, "utf8")),
		...process.env,
	};
	const setting = (name: string, fallback: string) =>
		env[`RUSTFS_${name}`] ?? env[`MINIO_${name}`] ?? fallback;

	const connection = Object.fromEntries(
		(env.ConnectionStrings__Default ?? "")
			.split(";")
			.filter((part) => part.includes("="))
			.map((part) => {
				const [key = "", ...value] = part.split("=");
				return [key.trim().toLowerCase(), value.join("=").trim()];
			}),
	);
	const credentials = `${encodeURIComponent(connection.username ?? "")}:${encodeURIComponent(connection.password ?? "")}`;
	const scheme = setting("USE_SSL", "false") === "true" ? "https" : "http";

	log(`Reading development stack from ${file}`);
	return {
		databaseUrl: `postgresql://${credentials}@${connection.host ?? "localhost"}:${connection.port ?? "5432"}/${connection.database ?? ""}`,
		storageUrl: `${scheme}://${setting("ENDPOINT", "localhost:9000")}`,
		accessKey: setting("ACCESS_KEY", "admin"),
		secretKey: setting("SECRET_KEY", "admin_pw"),
		buckets: {
			assets: setting("BUCKET_ASSETS", "beatdash-assets"),
			userData: setting("BUCKET_USER_DATA", "beatdash-user-data"),
		},
	};
}

async function copyIntoStaging(databaseUrl: string): Promise<void> {
	log("Copying development database into staging");
	await psql("postgres", [
		"-c",
		`DROP DATABASE IF EXISTS ${stack.stagingDatabase} WITH (FORCE)`,
		"-c",
		`CREATE DATABASE ${stack.stagingDatabase}`,
	]);
	const staging = `postgresql://${stack.postgresUser}:${stack.postgresPassword}@127.0.0.1:${ports.postgres}/${stack.stagingDatabase}`;
	await tool({
		image: "postgres",
		script: [
			`pg_dump --no-owner --no-privileges --file /tmp/development.sql ${shellQuote(databaseUrl)}`,
			`psql -X -q -v ON_ERROR_STOP=1 --file /tmp/development.sql ${shellQuote(staging)} >/dev/null`,
		].join("\n"),
	});
}

async function dumpStaging(): Promise<void> {
	log("Dumping curated data");
	const gzip = createGzip({ level: 9 });
	const written = pipeline(gzip, createWriteStream(paths.databaseDump));
	await dbExec(
		[
			"pg_dump",
			"--data-only",
			"--disable-triggers",
			"--no-owner",
			"--no-privileges",
			'--exclude-table="__EFMigrationsHistory"',
			"-U",
			stack.postgresUser,
			stack.stagingDatabase,
		],
		{ output: gzip },
	);
	await written;
}

/** Newest play plus a fixed gap, which seeding later moves to the current week. */
async function referenceTime(): Promise<string> {
	const latest = await query(
		stack.stagingDatabase,
		`SELECT to_json(max(greatest("StartedAt", coalesce("EndedAt", "StartedAt")))) #>> '{}' FROM "PlaySessions"`,
	);
	return new Date(
		Date.parse(latest) + curation.referenceOffsetMs,
	).toISOString();
}

async function exportStorage(source: DevelopmentStack): Promise<void> {
	log("Copying covers and profile images");
	const work = await mkdtemp(join(tmpdir(), "beatdash-fixture-"));
	try {
		const keys = {
			assets: await query(
				stack.stagingDatabase,
				`SELECT "CoverImageKey" FROM "Beatmaps" WHERE "CoverImageKey" IS NOT NULL ORDER BY 1`,
			),
			userData: await query(
				stack.stagingDatabase,
				`SELECT k FROM "Users", unnest(ARRAY["AvatarKey", "BannerKey"]) k WHERE k IS NOT NULL ORDER BY 1`,
			),
		};
		const folders = Object.keys(
			storageFolders,
		) as (keyof typeof storageFolders)[];
		for (const folder of folders)
			await writeFile(
				join(work, `${storageFolders[folder]}.txt`),
				`${keys[folder]}\n`,
			);

		await tool({
			image: "mc",
			workDir: work,
			script: [
				`mc alias set development ${shellQuote(source.storageUrl)} ${shellQuote(source.accessKey)} ${shellQuote(source.secretKey)} >/dev/null`,
				...folders.map(
					(folder) =>
						`while read -r key; do [ -n "$key" ] && mc cp --quiet "development/${source.buckets[folder]}/$key" "/work/${storageFolders[folder]}/$key" >/dev/null || echo "missing object $key" >&2; done < /work/${storageFolders[folder]}.txt`,
				),
			].join("\n"),
		});

		await rm(paths.storage, { recursive: true, force: true });
		for (const folder of folders) {
			for (const key of keys[folder].split("\n").filter(Boolean)) {
				const from = join(work, storageFolders[folder], key);
				if (!existsSync(from)) continue;
				const to = join(paths.storage, storageFolders[folder], key);
				await mkdir(dirname(to), { recursive: true });
				if (folder !== "assets") {
					await copyFile(from, to);
					continue;
				}
				// Covers are only ever shown as thumbnails; shrinking them keeps the fixture small.
				await sharp(from)
					.resize(curation.coverSize, curation.coverSize)
					.png({ palette: true, compressionLevel: 9 })
					.toFile(to);
			}
		}
	} finally {
		await rm(work, { recursive: true, force: true }).catch(() => undefined);
	}
}

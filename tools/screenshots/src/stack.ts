import { createReadStream } from "node:fs";
import { createGunzip } from "node:zlib";
import { migrationScript } from "./api.ts";
import { paths, ports, stack } from "./config.ts";
import { compose, psql, tool } from "./containers.ts";
import { type FixtureManifest, storageFolders } from "./fixture.ts";
import { log } from "./process.ts";

const week = 7 * 24 * 60 * 60 * 1000;

export async function startStack(): Promise<void> {
	log("Starting fixture Postgres and RustFS");
	await compose(["up", "--detach", "--wait", "db", "storage"]);
}

export async function stopStack(): Promise<void> {
	log("Removing fixture containers");
	await compose(["down", "--volumes", "--remove-orphans"]);
}

/**
 * Moment the fixture is staged at after seeding: its reference time moved forward by
 * whole weeks, so weekday-based charts keep their shape, to the latest point not after now.
 */
export function stagedTime(manifest: FixtureManifest, now = Date.now()): Date {
	const reference = Date.parse(manifest.referenceTime);
	const weeks = Math.floor((now - reference) / week);
	return new Date(reference + weeks * week);
}

/** Replaces the fixture database and buckets with the committed fixture. */
export async function seed(
	manifest: FixtureManifest,
	env: NodeJS.ProcessEnv,
): Promise<Date> {
	log(`Recreating database at migration ${manifest.migration}`);
	await psql("postgres", [
		"-c",
		`DROP DATABASE IF EXISTS ${stack.database} WITH (FORCE)`,
		"-c",
		`CREATE DATABASE ${stack.database}`,
	]);
	await runSqlFile(await migrationScript(env, "0", manifest.migration));

	log("Loading fixture data");
	await psql(stack.database, [], {
		input: createReadStream(paths.databaseDump).pipe(createGunzip()),
	});

	log("Applying newer migrations");
	await runSqlFile(await migrationScript(env, manifest.migration));

	const staged = stagedTime(manifest);
	const shiftMs = staged.getTime() - Date.parse(manifest.referenceTime);
	log(`Staging data at ${staged.toISOString()}`);
	await runSqlFile(paths.shiftTimeSql, [
		"-v",
		`shift=${shiftMs / 1000} seconds`,
	]);

	await seedStorage();
	return staged;
}

async function runSqlFile(
	file: string,
	args: readonly string[] = [],
): Promise<void> {
	await psql(stack.database, args, { input: createReadStream(file) });
}

async function seedStorage(): Promise<void> {
	log("Uploading fixture objects");
	const buckets = [
		[storageFolders.assets, stack.bucketAssets],
		[storageFolders.userData, stack.bucketUserData],
	] as const;
	const mirror = buckets.map(
		([folder, bucket]) =>
			`mc mb --ignore-existing fixture/${bucket} && mc mirror --quiet --overwrite --remove /work/${folder} fixture/${bucket}`,
	);
	await tool({
		image: "mc",
		workDir: paths.storage,
		readOnly: true,
		script: [
			`mc alias set fixture http://127.0.0.1:${ports.storage} ${stack.storageAccessKey} ${stack.storageSecretKey} >/dev/null`,
			...mirror,
		].join("\n"),
	});
}

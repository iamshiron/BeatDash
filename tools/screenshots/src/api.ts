import { type ChildProcess, spawn } from "node:child_process";
import { mkdir, readFile } from "node:fs/promises";
import { join } from "node:path";
import { parseEnv } from "node:util";
import { apiUrl, paths, ports, stack } from "./config.ts";
import type { FixtureManifest } from "./fixture.ts";
import { log, run } from "./process.ts";

const startupTimeoutMs = 90_000;
const retainedLogLines = 80;

/** Environment for the API and the EF tooling, pointed at the disposable stack only. */
export async function apiEnvironment(
	manifest: FixtureManifest,
): Promise<NodeJS.ProcessEnv> {
	// Metric calibration is configuration, not data; use the committed one so stored
	// difficulty ratings are not re-scored in the background while capturing.
	const calibration = Object.entries(
		parseEnv(await readFile(paths.envExample, "utf8")),
	).filter(([key]) => key.startsWith("Metrics__"));

	return {
		...process.env,
		...Object.fromEntries(calibration),
		ASPNETCORE_ENVIRONMENT: "Development",
		ASPNETCORE_URLS: apiUrl,
		ConnectionStrings__Default: `Host=127.0.0.1;Port=${ports.postgres};Database=${stack.database};Username=${stack.postgresUser};Password=${stack.postgresPassword}`,
		Jwt__SecretKey: "screenshot-fixture-only-secret-key-not-for-real-use",
		Jwt__Issuer: "BeatDash",
		Jwt__Audience: "BeatDash",
		AdminUser__Email: manifest.login.email,
		AdminUser__Password: manifest.login.password,
		AdminUser__Role: "Admin",
		Backup__MaintenanceMode: "false",
		Backup__WorkDirectory: join(paths.apiWorkDir, "backup-work"),
		RUSTFS_ENDPOINT: `127.0.0.1:${ports.storage}`,
		RUSTFS_ACCESS_KEY: stack.storageAccessKey,
		RUSTFS_SECRET_KEY: stack.storageSecretKey,
		RUSTFS_USE_SSL: "false",
		RUSTFS_BUCKET_ASSETS: stack.bucketAssets,
		RUSTFS_BUCKET_USER_DATA: stack.bucketUserData,
		UdpSocket__Host: "127.0.0.1",
		UdpSocket__Port: String(ports.api),
		Hsp__PublicIngestUrl: "",
		BeatSaver__FetchOnStartup: "false",
		BeatSaver__ScheduledFetchEnabled: "false",
		BeatSaver__FetchOnNewMap: "false",
	};
}

/** Builds the API in Release, which also builds the web client into its wwwroot. */
export async function buildApi(): Promise<void> {
	log("Building API and web client (Release)");
	await run("dotnet", [
		"build",
		paths.apiProject,
		"--configuration",
		"Release",
		"--nologo",
		"-verbosity:quiet",
	]);
}

/**
 * Writes the SQL that migrates the schema from one EF Core migration to another
 * ("0" is the empty database, omitting the target means the latest migration).
 */
export async function migrationScript(
	env: NodeJS.ProcessEnv,
	from: string,
	to?: string,
): Promise<string> {
	await mkdir(paths.apiWorkDir, { recursive: true });
	const output = join(
		paths.apiWorkDir,
		`migrate-${from}-${to ?? "latest"}.sql`,
	);
	await run(
		"dotnet",
		[
			"ef",
			"migrations",
			"script",
			from,
			...(to ? [to] : []),
			"--project",
			paths.dbProject,
			"--startup-project",
			paths.apiProject,
			"--configuration",
			"Release",
			"--no-build",
			"--output",
			output,
		],
		{ cwd: paths.apiWorkDir, env },
	);
	return output;
}

export interface RunningApi {
	stop(): Promise<void>;
}

/** Starts the built API and resolves once it answers its health check. */
export async function startApi(env: NodeJS.ProcessEnv): Promise<RunningApi> {
	if (await isHealthy())
		throw new Error(
			`Something is already listening on ${apiUrl}; set SCREENSHOTS_API_PORT.`,
		);

	await mkdir(paths.apiWorkDir, { recursive: true });
	log(`Starting API on ${apiUrl}`);
	const child = spawn("dotnet", [paths.apiAssembly], {
		cwd: paths.apiWorkDir,
		env,
		stdio: ["ignore", "pipe", "pipe"],
	});

	const output: string[] = [];
	const collect = (chunk: Buffer) => {
		output.push(...chunk.toString().split("\n"));
		output.splice(0, Math.max(0, output.length - retainedLogLines));
	};
	child.stdout?.on("data", collect);
	child.stderr?.on("data", collect);
	const exited = new Promise<number | null>((resolve) =>
		child.once("exit", resolve),
	);

	const deadline = Date.now() + startupTimeoutMs;
	while (!(await isHealthy())) {
		const exitCode = await Promise.race([
			exited,
			delay(500).then(() => undefined),
		]);
		if (exitCode !== undefined || Date.now() > deadline) {
			child.kill("SIGKILL");
			throw new Error(
				`API failed to start (exit code ${exitCode ?? "timeout"}):\n${output.join("\n")}`,
			);
		}
	}

	return { stop: () => stop(child, exited) };
}

async function stop(
	child: ChildProcess,
	exited: Promise<number | null>,
): Promise<void> {
	if (child.exitCode !== null) return;
	child.kill("SIGINT");
	const forced = delay(10_000).then(() => child.kill("SIGKILL"));
	await Promise.race([exited, forced]);
}

async function isHealthy(): Promise<boolean> {
	try {
		const response = await fetch(`${apiUrl}/api/health`, {
			signal: AbortSignal.timeout(2_000),
		});
		return response.ok;
	} catch {
		return false;
	}
}

function delay(ms: number): Promise<void> {
	return new Promise((resolve) => setTimeout(resolve, ms));
}

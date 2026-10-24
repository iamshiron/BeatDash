import type { Readable, Writable } from "node:stream";
import { images, paths, stack } from "./config.ts";
import { isAvailable, run } from "./process.ts";

let cli: Promise<string> | undefined;

/** Docker or Podman, overridable through SCREENSHOTS_CONTAINER_CLI. */
function containerCli(): Promise<string> {
	cli ??= (async () => {
		const configured = process.env.SCREENSHOTS_CONTAINER_CLI;
		if (configured) return configured;
		if (await isAvailable("docker")) return "docker";
		if (await isAvailable("podman")) return "podman";
		throw new Error("Neither docker nor podman is available.");
	})();
	return cli;
}

export async function compose(args: readonly string[]): Promise<string> {
	return run(await containerCli(), ["compose", "-f", paths.compose, ...args]);
}

export interface Streams {
	readonly input?: Readable;
	readonly output?: Writable;
}

/** Runs a command inside the fixture database container. */
export async function dbExec(
	command: readonly string[],
	streams: Streams = {},
): Promise<string> {
	return run(
		await containerCli(),
		["compose", "-f", paths.compose, "exec", "-T", "db", ...command],
		streams,
	);
}

/** Runs SQL against a database of the fixture Postgres. */
export async function psql(
	database: string,
	args: readonly string[],
	streams: Streams = {},
): Promise<string> {
	const connection = [
		"-U",
		stack.postgresUser,
		"-d",
		database,
		"-v",
		"ON_ERROR_STOP=1",
		"-X",
		"-q",
	];
	return dbExec(["psql", ...connection, ...args], streams);
}

/** Runs a single query and returns its unaligned, header-less result. */
export async function query(database: string, sql: string): Promise<string> {
	return (await psql(database, ["-A", "-t", "-c", sql])).trim();
}

export interface ToolRun {
	readonly image: keyof typeof images;
	readonly script: string;
	/** Host directory mounted at /work. */
	readonly workDir?: string;
	readonly readOnly?: boolean;
	readonly output?: Writable;
}

/**
 * Runs a shell script in a one-off container on the host network, so it reaches the
 * development stack and the fixture stack through their published ports alike.
 */
export async function tool({
	image,
	script,
	workDir,
	readOnly,
	output,
}: ToolRun): Promise<string> {
	const mount = workDir
		? ["-v", `${workDir}:/work:${readOnly ? "ro," : ""}z`]
		: [];
	const args = [
		"run",
		"--rm",
		"--network",
		"host",
		...mount,
		"--entrypoint",
		"/bin/sh",
		images[image],
		"-ec",
		script,
	];
	return run(await containerCli(), args, { output });
}

/** Quotes a value for a POSIX shell. */
export function shellQuote(value: string): string {
	return `'${value.replaceAll("'", `'\\''`)}'`;
}

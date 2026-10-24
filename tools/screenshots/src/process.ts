import { spawn } from "node:child_process";
import type { Readable, Writable } from "node:stream";
import { pipeline } from "node:stream/promises";

export interface RunOptions {
	readonly cwd?: string;
	readonly env?: NodeJS.ProcessEnv;
	/** Streamed into the process' stdin. */
	readonly input?: Readable;
	/** Receives stdout instead of it being collected and returned. */
	readonly output?: Writable;
}

/** Runs a command to completion, returning its stdout. Fails with the captured stderr. */
export async function run(
	command: string,
	args: readonly string[],
	options: RunOptions = {},
): Promise<string> {
	const child = spawn(command, args, {
		cwd: options.cwd,
		env: options.env ?? process.env,
		stdio: "pipe",
	});

	const stdout: Buffer[] = [];
	const stderr: Buffer[] = [];
	child.stderr.on("data", (chunk: Buffer) => stderr.push(chunk));
	const streams: Promise<void>[] = [];
	if (options.output) streams.push(pipeline(child.stdout, options.output));
	else child.stdout.on("data", (chunk: Buffer) => stdout.push(chunk));
	if (options.input) streams.push(pipeline(options.input, child.stdin));
	else child.stdin.end();

	const exitCode = await new Promise<number | null>((resolve, reject) => {
		child.once("error", reject);
		child.once("close", resolve);
	});
	await Promise.all(streams);

	if (exitCode !== 0) {
		const details =
			Buffer.concat(stderr).toString().trim() ||
			Buffer.concat(stdout).toString().trim();
		throw new Error(
			`${command} ${args.join(" ")} exited with ${exitCode}\n${details}`,
		);
	}
	return Buffer.concat(stdout).toString();
}

/** Whether a command can be launched at all. */
export async function isAvailable(command: string): Promise<boolean> {
	try {
		await run(command, ["--version"]);
		return true;
	} catch {
		return false;
	}
}

export function log(message: string): void {
	console.log(`\u001b[35m›\u001b[0m ${message}`);
}

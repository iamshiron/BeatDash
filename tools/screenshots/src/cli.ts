import { once } from "node:events";
import { parseArgs } from "node:util";
import { apiEnvironment, buildApi, startApi } from "./api.ts";
import { capture } from "./capture.ts";
import { apiUrl } from "./config.ts";
import { exportFixture } from "./export.ts";
import { type FixtureManifest, readManifest } from "./fixture.ts";
import { log } from "./process.ts";
import type { Shot } from "./shot.ts";
import { shots } from "./shots.ts";
import { seed, startStack, stopStack } from "./stack.ts";

const usage = `Usage: pnpm screenshots [command | shot...] [options]

  (no arguments)   Capture every screenshot into assets/
  <shot>...        Capture only the named screenshots
  list             List the available screenshots
  serve            Seed the fixture and keep the app running for browsing
  export           Rebuild the fixture from the development stack (.env)
  down             Remove the fixture containers

Options:
  --no-build       Reuse the existing Release build of the API and web client
  --keep           Leave the fixture containers running afterwards
  -h, --help       Show this help`;

const commands = ["list", "serve", "export", "down"] as const;
type Command = (typeof commands)[number];

interface Options {
	readonly build: boolean;
	readonly keep: boolean;
}

const { values, positionals } = parseArgs({
	allowPositionals: true,
	options: {
		"no-build": { type: "boolean", default: false },
		keep: { type: "boolean", default: false },
		help: { type: "boolean", short: "h", default: false },
	},
});

try {
	await main(positionals, { build: !values["no-build"], keep: values.keep });
} catch (error) {
	console.error(
		`\u001b[31m✖\u001b[0m ${error instanceof Error ? error.message : String(error)}`,
	);
	process.exitCode = 1;
}

async function main(args: readonly string[], options: Options): Promise<void> {
	const [first, ...rest] = args;
	if (values.help) {
		console.log(usage);
	} else if (isCommand(first)) {
		if (rest.length > 0)
			throw new Error(`'${first}' takes no further arguments.\n\n${usage}`);
		await runCommand(first, options);
	} else {
		await captureShots(selectShots(args), options);
	}
}

function isCommand(value: string | undefined): value is Command {
	return commands.some((command) => command === value);
}

async function runCommand(command: Command, options: Options): Promise<void> {
	switch (command) {
		case "list":
			for (const shot of shots)
				console.log(`${shot.name.padEnd(16)} assets/${shot.output}`);
			return;
		case "serve":
			return serve(options);
		case "export":
			return withStack(options, exportFixture);
		case "down":
			return stopStack();
	}
}

function selectShots(names: readonly string[]): readonly Shot[] {
	if (names.length === 0) return shots;
	const unknown = names.filter(
		(name) => !shots.some((shot) => shot.name === name),
	);
	if (unknown.length > 0) {
		const available = shots.map((shot) => shot.name).join(", ");
		throw new Error(
			`Unknown screenshot ${unknown.join(", ")}. Available: ${available}`,
		);
	}
	return shots.filter((shot) => names.includes(shot.name));
}

async function captureShots(
	selected: readonly Shot[],
	options: Options,
): Promise<void> {
	await withApp(options, async (manifest, now) =>
		capture(selected, { manifest, now }),
	);
}

async function serve(options: Options): Promise<void> {
	await withApp(options, async (manifest) => {
		log(`BeatDash is running at ${apiUrl}`);
		log(
			`Sign in as ${manifest.login.email} / ${manifest.login.password}. Press Ctrl+C to stop.`,
		);
		await once(process, "SIGINT");
	});
}

/** Seeds the fixture stack and runs the API for the duration of `body`. */
async function withApp(
	options: Options,
	body: (manifest: FixtureManifest, now: Date) => Promise<void>,
): Promise<void> {
	const manifest = await readManifest();
	const env = await apiEnvironment(manifest);
	await withStack(options, async () => {
		if (options.build) await buildApi();
		const now = await seed(manifest, env);
		const api = await startApi(env);
		try {
			await body(manifest, now);
		} finally {
			await api.stop();
		}
	});
}

async function withStack(
	options: Options,
	body: () => Promise<void>,
): Promise<void> {
	await startStack();
	try {
		await body();
	} finally {
		if (!options.keep) await stopStack();
	}
}

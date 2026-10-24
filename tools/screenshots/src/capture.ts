import { mkdir, writeFile } from "node:fs/promises";
import { join } from "node:path";
import {
	type Browser,
	type BrowserContext,
	chromium,
	type Page,
} from "playwright";
import sharp from "sharp";
import { apiUrl, paths } from "./config.ts";
import type { FixtureManifest } from "./fixture.ts";
import { log } from "./process.ts";
import type { ApiClient, Shot } from "./shot.ts";

const defaultViewport = { width: 1440, height: 900 } as const;
const theme = { storageKey: "shiron-ui-theme", name: "amethyst" } as const;
/** Corner radius of the window frame the README images are cut into. */
const frameRadius = 40;
const skeletonTimeoutMs = 20_000;
const imageTimeoutMs = 10_000;
/** Lets charts finish their entry animations, which run on timers rather than CSS. */
const settleMs = 1_500;

export interface CaptureOptions {
	readonly manifest: FixtureManifest;
	/** Fixed browser clock, matching the moment the fixture data is staged at. */
	readonly now: Date;
}

export async function capture(
	shots: readonly Shot[],
	options: CaptureOptions,
): Promise<void> {
	await mkdir(paths.output, { recursive: true });
	const browser = await chromium.launch();
	try {
		for (const shot of shots) await captureShot(browser, shot, options);
	} finally {
		await browser.close();
	}
}

async function captureShot(
	browser: Browser,
	shot: Shot,
	{ manifest, now }: CaptureOptions,
): Promise<void> {
	const context = await browser.newContext({
		baseURL: apiUrl,
		viewport: shot.viewport ?? defaultViewport,
		deviceScaleFactor: 1,
		locale: "en-US",
		timezoneId: "UTC",
		colorScheme: "dark",
		reducedMotion: "reduce",
	});
	try {
		await context.clock.setFixedTime(now);
		await context.addInitScript(
			({ storageKey, name }) => localStorage.setItem(storageKey, name),
			theme,
		);
		if (shot.signedIn) await signIn(context, manifest);

		const path =
			typeof shot.path === "string"
				? shot.path
				: await shot.path(apiClient(context));
		const page = await context.newPage();
		await page.goto(path);
		await settle(page, shot.name);
		if (shot.prepare) {
			await shot.prepare(page);
			await settle(page, shot.name);
		}

		const screenshot = await page.screenshot({
			animations: "disabled",
			fullPage: shot.fullPage ?? false,
		});
		const output = join(paths.output, shot.output);
		await writeFile(output, await frame(screenshot));
		log(`${shot.name} → ${output}`);
	} finally {
		await context.close();
	}
}

async function signIn(
	context: BrowserContext,
	manifest: FixtureManifest,
): Promise<void> {
	const response = await context.request.post("/api/auth/login", {
		data: { ...manifest.login, rememberMe: true },
	});
	if (!response.ok())
		throw new Error(
			`Signing in as ${manifest.login.email} failed with ${response.status()}.`,
		);
}

function apiClient(context: BrowserContext): ApiClient {
	return {
		async get<T>(path: string): Promise<T> {
			const response = await context.request.get(path);
			if (!response.ok())
				throw new Error(`GET ${path} failed with ${response.status()}.`);
			return (await response.json()) as T;
		},
	};
}

/** Waits until the page shows data: requests done, skeletons gone, fonts and images loaded. */
async function settle(page: Page, name: string): Promise<void> {
	await page.waitForLoadState("networkidle");
	try {
		await page.waitForFunction(
			() => document.querySelector('[data-slot="skeleton"]') === null,
			undefined,
			{
				timeout: skeletonTimeoutMs,
			},
		);
	} catch {
		log(`${name}: loading skeletons are still visible, capturing anyway`);
	}
	await page.evaluate(async (timeout) => {
		await document.fonts.ready;
		const pending = [...document.images]
			.filter((image) => !image.complete)
			.map(
				(image) =>
					new Promise((resolve) => {
						image.addEventListener("load", resolve, { once: true });
						image.addEventListener("error", resolve, { once: true });
					}),
			);
		await Promise.race([
			Promise.all(pending),
			new Promise((resolve) => setTimeout(resolve, timeout)),
		]);
	}, imageTimeoutMs);
	await page.waitForTimeout(settleMs);
}

/** Cuts the screenshot into a rounded window shape on a transparent background. */
async function frame(screenshot: Buffer): Promise<Buffer> {
	const { width, height } = await sharp(screenshot).metadata();
	const mask = `<svg width="${width}" height="${height}"><rect width="${width}" height="${height}" rx="${frameRadius}"/></svg>`;
	return sharp(screenshot)
		.ensureAlpha()
		.composite([{ input: Buffer.from(mask), blend: "dest-in" }])
		.png({ compressionLevel: 9 })
		.toBuffer();
}

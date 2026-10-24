import { pbkdf2Sync, randomBytes } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import { paths } from "./config.ts";

/** Describes the committed fixture in fixture/fixture.json. */
export interface FixtureManifest {
	/** Last EF Core migration applied to the schema the dump was taken from. */
	readonly migration: string;
	/** Moment the fixture data is staged at; seeding moves it to the current week. */
	readonly referenceTime: string;
	readonly login: {
		readonly email: string;
		readonly password: string;
	};
}

/** Object storage layout of the fixture: directory under fixture/storage per bucket. */
export const storageFolders = {
	assets: "assets",
	userData: "user-data",
} as const;

export async function readManifest(): Promise<FixtureManifest> {
	return JSON.parse(await readFile(paths.manifest, "utf8")) as FixtureManifest;
}

export async function writeManifest(manifest: FixtureManifest): Promise<void> {
	await writeFile(paths.manifest, `${JSON.stringify(manifest, null, 4)}\n`);
}

const identityHash = {
	formatMarker: 0x01,
	prfHmacSha512: 2,
	iterations: 100_000,
	saltLength: 16,
	subkeyLength: 32,
} as const;

/** Hashes a password in the ASP.NET Core Identity V3 format (PBKDF2, HMAC-SHA512). */
export function hashIdentityPassword(password: string): string {
	const salt = randomBytes(identityHash.saltLength);
	const subkey = pbkdf2Sync(
		password,
		salt,
		identityHash.iterations,
		identityHash.subkeyLength,
		"sha512",
	);
	const header = Buffer.alloc(13);
	header.writeUInt8(identityHash.formatMarker, 0);
	header.writeUInt32BE(identityHash.prfHmacSha512, 1);
	header.writeUInt32BE(identityHash.iterations, 5);
	header.writeUInt32BE(identityHash.saltLength, 9);
	return Buffer.concat([header, salt, subkey]).toString("base64");
}

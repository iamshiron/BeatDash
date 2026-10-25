import { existsSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import type { Plugin } from 'vite';

/** Emitted next to index.html and merged into the server's THIRD-PARTY-NOTICES.txt. */
export const ThirdPartyNoticesFileName = 'third-party-notices.txt';

const NodeModulesSegment = '/node_modules/';
const LicenseFilePattern = /^(licen[cs]e|copying|notice)([.-].*)?$/i;
const SpdxTemplateDirectory = path.resolve(import.meta.dirname, '../../tools/release/licenses');

interface PackageManifest {
    readonly name: string;
    readonly version: string;
    readonly license?: string;
    readonly homepage?: string;
    readonly author?: string | { readonly name: string };
}

interface BundledPackage {
    readonly manifest: PackageManifest;
    readonly licenseText: string;
}

/** Resolves `.../node_modules/@scope/name/dist/x.js` to the package directory. */
function packageRoot(file: string): string | undefined {
    const normalized = file.replaceAll('\\', '/').split('?')[0] ?? '';
    const index = normalized.lastIndexOf(NodeModulesSegment);
    if (normalized.startsWith('\0') || index < 0) return undefined;
    const base = normalized.slice(0, index + NodeModulesSegment.length);
    const segments = normalized.slice(base.length).split('/');
    const depth = segments[0]?.startsWith('@') ? 2 : 1;
    return base + segments.slice(0, depth).join('/');
}

function licenseText(root: string, manifest: PackageManifest): string {
    const files = readdirSync(root).filter((name) => LicenseFilePattern.test(name)).sort();
    if (files.length > 0) {
        return files.map((name) => readFileSync(path.join(root, name), 'utf8').trim()).join('\n\n');
    }
    // Every identifier of an expression such as "(MIT OR CC0-1.0)" or "MIT AND ISC".
    const identifiers = (manifest.license ?? '').split(/[\s()]+|\bAND\b|\bOR\b/).filter(Boolean);
    const missing = identifiers.filter((id) => !existsSync(path.join(SpdxTemplateDirectory, `${id}.txt`)));
    if (identifiers.length === 0 || missing.length > 0) {
        throw new Error(
            `${manifest.name}@${manifest.version} ships no license file and no SPDX template exists for ` +
                `'${missing.join(', ') || manifest.license}'. Add it to tools/release/licenses (see its README).`,
        );
    }
    return identifiers
        .map((id) => readFileSync(path.join(SpdxTemplateDirectory, `${id}.txt`), 'utf8').trim())
        .join('\n\n');
}

function readPackage(root: string): BundledPackage {
    const manifest = JSON.parse(readFileSync(path.join(root, 'package.json'), 'utf8')) as PackageManifest;
    return { manifest, licenseText: licenseText(root, manifest) };
}

function format(packages: readonly BundledPackage[]): string {
    const sections = packages.map(({ manifest, licenseText: text }) =>
        [
            `${manifest.name} ${manifest.version}`,
            `License: ${manifest.license ?? 'see below'}`,
            ...(manifest.author ? [`Author: ${typeof manifest.author === 'string' ? manifest.author : manifest.author.name}`] : []),
            ...(manifest.homepage ? [`Homepage: ${manifest.homepage}`] : []),
            '',
            text,
        ].join('\n'),
    );
    const separator = `\n\n${'='.repeat(80)}\n\n`;
    return `Third-party software bundled in the BeatDash web dashboard${separator}${sections.join(separator)}\n`;
}

/** Writes the licenses of every npm package and asset that ends up in the production bundle. */
export function thirdPartyNotices(): Plugin {
    return {
        name: 'beatdash:third-party-notices',
        apply: 'build',
        generateBundle(_options, bundle) {
            const files = Object.values(bundle).flatMap((output) =>
                output.type === 'chunk' ? output.moduleIds : output.originalFileNames,
            );
            const roots = [...new Set(files.map(packageRoot).filter((root) => root !== undefined))];
            const packages = roots
                .map(readPackage)
                .sort((a, b) => a.manifest.name.localeCompare(b.manifest.name) || a.manifest.version.localeCompare(b.manifest.version));
            this.emitFile({ type: 'asset', fileName: ThirdPartyNoticesFileName, source: format(packages) });
        },
    };
}

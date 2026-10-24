import type { Page } from "playwright";

/** Read access to the BeatDash API, authenticated like the page being captured. */
export interface ApiClient {
	get<T>(path: string): Promise<T>;
}

/** One screenshot of the web app. */
export interface Shot {
	/** Name used on the command line, e.g. `pnpm screenshots dashboard`. */
	readonly name: string;
	/** File written to the repository's assets folder. */
	readonly output: string;
	/** Captures as the fixture account when true, as an anonymous visitor otherwise. */
	readonly signedIn: boolean;
	/** Route to open, or a lookup for routes that need an id from the fixture. */
	readonly path: string | ((api: ApiClient) => Promise<string>);
	/** Interaction before capturing, such as opening a tab or scrolling to a section. */
	readonly prepare?: (page: Page) => Promise<void>;
	/** Defaults to 1440×900. */
	readonly viewport?: { readonly width: number; readonly height: number };
	/** Captures the whole scrollable page instead of the viewport. */
	readonly fullPage?: boolean;
}

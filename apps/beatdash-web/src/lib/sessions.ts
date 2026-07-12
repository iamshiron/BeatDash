import type { GetApiSessionsParams } from "@/api/model";

export const DIFFICULTY_STYLES: Record<string, string> = {
	Easy: "border-emerald-500/25 bg-emerald-500/15 text-emerald-400",
	Normal: "border-sky-500/25 bg-sky-500/15 text-sky-400",
	Hard: "border-amber-500/25 bg-amber-500/15 text-amber-400",
	Expert: "border-rose-500/25 bg-rose-500/15 text-rose-400",
	ExpertPlus: "border-violet-500/25 bg-violet-500/15 text-violet-400",
};

export const DIFFICULTY_TEXT_STYLES: Record<string, string> = {
	Easy: "text-emerald-400",
	Normal: "text-sky-400",
	Hard: "text-amber-400",
	Expert: "text-rose-400",
	ExpertPlus: "text-violet-400",
};

export const RANK_STYLES: Record<string, string> = {
	SSS: "text-amber-300",
	SS: "text-amber-400",
	S: "text-fuchsia-400",
	A: "text-sky-400",
	B: "text-emerald-400",
	C: "text-yellow-400",
	D: "text-orange-400",
	E: "text-red-400",
};

export const DIFFICULTY_OPTIONS = [
	{ value: "Easy", label: "Easy" },
	{ value: "Normal", label: "Normal" },
	{ value: "Hard", label: "Hard" },
	{ value: "Expert", label: "Expert" },
	{ value: "ExpertPlus", label: "Expert+" },
] as const;

export const SORT_OPTIONS = [
	{ value: "StartedAt", label: "Date" },
	{ value: "Score", label: "Score" },
	{ value: "Accuracy", label: "Accuracy" },
	{ value: "Duration", label: "Duration" },
	{ value: "MaxCombo", label: "Max Combo" },
] as const;

export const SORT_OPTIONS_COMBINED = [
	{ value: "StartedAt:Desc", label: "Newest first" },
	{ value: "StartedAt:Asc", label: "Oldest first" },
	{ value: "Score:Desc", label: "Highest score" },
	{ value: "Score:Asc", label: "Lowest score" },
	{ value: "Accuracy:Desc", label: "Highest accuracy" },
	{ value: "Accuracy:Asc", label: "Lowest accuracy" },
	{ value: "Duration:Desc", label: "Longest duration" },
	{ value: "Duration:Asc", label: "Shortest duration" },
	{ value: "MaxCombo:Desc", label: "Longest combo" },
	{ value: "MaxCombo:Asc", label: "Shortest combo" },
] as const;

/** Grade options for the rank filter, best → worst (mirrors {@link RANK_STYLES} keys). */
export const RANK_OPTIONS = [
	{ value: "SSS", label: "SSS" },
	{ value: "SS", label: "SS" },
	{ value: "S", label: "S" },
	{ value: "A", label: "A" },
	{ value: "B", label: "B" },
	{ value: "C", label: "C" },
	{ value: "D", label: "D" },
	{ value: "E", label: "E" },
] as const;

const RANK_VALUES = new Set(RANK_OPTIONS.map((o) => o.value));

/** Whitelist a raw rank value from the URL against the known grades. */
export function normalizeRank(value: string | undefined): string | undefined {
	return value &&
		RANK_VALUES.has(value as (typeof RANK_OPTIONS)[number]["value"])
		? value
		: undefined;
}

/**
 * Play-outcome toggles. Each maps to an `Include*` API flag; "Finished" plays are
 * always included server-side, so only the opt-in outcomes are listed here.
 */
export const OUTCOME_OPTIONS = [
	{ key: "auto", label: "Auto" },
	{ key: "fail", label: "Failed" },
	{ key: "quit", label: "Quit" },
	{ key: "inc", label: "Incomplete" },
] as const satisfies readonly {
	key: keyof SessionSearchParams;
	label: string;
}[];

const DIFFICULTY_TO_NUM: Record<string, number> = {
	Easy: 0,
	Normal: 1,
	Hard: 2,
	Expert: 3,
	ExpertPlus: 4,
};

const SORT_TO_NUM: Record<string, number> = {
	StartedAt: 0,
	Score: 1,
	Accuracy: 2,
	Duration: 3,
	MaxCombo: 4,
};

/**
 * URL-facing search/filter state for the sessions list. Keys are deliberately
 * terse — they are serialized verbatim as query-string parameters, so short
 * names keep shareable links compact.
 */
export interface SessionSearchParams {
	page?: number;
	/** Free-text search across song / author / mapper. */
	q?: string;
	/** Difficulty rank name, e.g. `Expert`. */
	diff?: string;
	/** Sort field, e.g. `StartedAt`. */
	sort?: string;
	/** Sort direction, `Asc` or `Desc`. */
	dir?: string;
	/** Include auto-play sessions. */
	auto?: boolean;
	/** Include failed sessions. */
	fail?: boolean;
	/** Include quit sessions. */
	quit?: boolean;
	/** Include incomplete sessions. */
	inc?: boolean;
	/** Inclusive start of the played-date range, as a `YYYY-MM-DD` string. */
	from?: string;
	/** Inclusive end of the played-date range, as a `YYYY-MM-DD` string. */
	to?: string;
	/** Min accuracy in whole percent (0–100); converted to a 0–1 ratio for the API. */
	amin?: number;
	/** Max accuracy in whole percent (0–100). */
	amax?: number;
	/** Min score. */
	smin?: number;
	/** Max score. */
	smax?: number;
	/** Grade filter, e.g. `S`. */
	rank?: string;
	/** Full-combo plays only. */
	fc?: boolean;
	/** Min BPM. */
	bmin?: number;
	/** Max BPM. */
	bmax?: number;
	/** Show the post-session recap surface (set when arriving from a just-finished play). */
	recap?: boolean;
}

export function toApiParams(search: SessionSearchParams): GetApiSessionsParams {
	return {
		Page: search.page ?? 1,
		PageSize: 25,
		Search: search.q?.trim() || undefined,
		Difficulty: search.diff ? DIFFICULTY_TO_NUM[search.diff] : undefined,
		SortBy: SORT_TO_NUM[search.sort ?? "StartedAt"] ?? 0,
		SortDir: (search.dir ?? "Desc") === "Asc" ? 0 : 1,
		IncludeAuto: search.auto ?? false,
		IncludeFailed: search.fail ?? false,
		IncludeQuit: search.quit ?? false,
		IncludeIncomplete: search.inc ?? false,
		// StartedAt is stored in UTC; expand the day-only bounds to a full inclusive range.
		From: search.from ? `${search.from}T00:00:00.000Z` : undefined,
		To: search.to ? `${search.to}T23:59:59.999Z` : undefined,
		MinAccuracy: search.amin != null ? search.amin / 100 : undefined,
		MaxAccuracy: search.amax != null ? search.amax / 100 : undefined,
		MinScore: search.smin,
		MaxScore: search.smax,
		Rank: search.rank || undefined,
		FullComboOnly: search.fc ?? false,
		MinBpm: search.bmin,
		MaxBpm: search.bmax,
	};
}

/**
 * The `SessionSearchParams` keys that represent user-facing filters (everything
 * except paging, search text, and sort). Used to detect "any filter active" and
 * to reset all filters at once.
 */
export const FILTER_KEYS = [
	"diff",
	"auto",
	"fail",
	"quit",
	"inc",
	"from",
	"to",
	"amin",
	"amax",
	"smin",
	"smax",
	"rank",
	"fc",
	"bmin",
	"bmax",
] as const satisfies readonly (keyof SessionSearchParams)[];

/** True when at least one map/session filter (not search or sort) is active. */
export function hasActiveFilters(search: SessionSearchParams): boolean {
	return getActiveFilters(search).length > 0;
}

/** A single active filter, rendered as a removable chip. */
export interface ActiveFilter {
	id: string;
	label: string;
	/** Search keys reset to `undefined` when this chip is removed. */
	keys: (keyof SessionSearchParams)[];
}

/**
 * Describes every active filter as a removable chip. Search text and sort are
 * intentionally excluded — they have their own dedicated controls.
 */
export function getActiveFilters(search: SessionSearchParams): ActiveFilter[] {
	const chips: ActiveFilter[] = [];

	if (search.diff) {
		const label =
			DIFFICULTY_OPTIONS.find((o) => o.value === search.diff)?.label ??
			search.diff;
		chips.push({ id: "diff", label, keys: ["diff"] });
	}

	if (search.from || search.to) {
		const label =
			search.from && search.to
				? `${search.from} – ${search.to}`
				: search.from
					? `From ${search.from}`
					: `Until ${search.to}`;
		chips.push({ id: "date", label, keys: ["from", "to"] });
	}

	if (search.amin != null || search.amax != null) {
		const label = rangeLabel(search.amin, search.amax, "%");
		chips.push({
			id: "accuracy",
			label: `Acc ${label}`,
			keys: ["amin", "amax"],
		});
	}

	if (search.smin != null || search.smax != null) {
		const label = rangeLabel(search.smin, search.smax);
		chips.push({
			id: "score",
			label: `Score ${label}`,
			keys: ["smin", "smax"],
		});
	}

	if (search.rank) {
		chips.push({ id: "rank", label: `Rank ${search.rank}`, keys: ["rank"] });
	}

	if (search.fc) {
		chips.push({ id: "fc", label: "Full combo", keys: ["fc"] });
	}

	if (search.bmin != null || search.bmax != null) {
		const label = rangeLabel(search.bmin, search.bmax);
		chips.push({ id: "bpm", label: `${label} BPM`, keys: ["bmin", "bmax"] });
	}

	for (const { key, label } of OUTCOME_OPTIONS) {
		if (search[key]) chips.push({ id: key, label, keys: [key] });
	}

	return chips;
}

/** Formats an optional numeric range as "≥ min", "≤ max", or "min–max" (+ optional unit). */
function rangeLabel(min?: number, max?: number, unit = ""): string {
	if (min != null && max != null) return `${min}–${max}${unit}`;
	if (min != null) return `≥ ${min}${unit}`;
	return `≤ ${max}${unit}`;
}

/**
 * Parses raw URL search params into a validated {@link SessionSearchParams}.
 * Shared by the list route and the detail route (whose back-link round-trips the
 * same state), so the two stay in lock-step.
 */
export function parseSessionSearch(
	search: Record<string, unknown>,
): SessionSearchParams {
	return {
		page: Math.max(1, Number(search.page) || 1),
		q: typeof search.q === "string" ? search.q : "",
		diff:
			typeof search.diff === "string" && search.diff !== "all"
				? search.diff
				: undefined,
		sort: typeof search.sort === "string" ? search.sort : "StartedAt",
		dir: typeof search.dir === "string" ? search.dir : "Desc",
		auto: asBool(search.auto),
		fail: asBool(search.fail),
		quit: asBool(search.quit),
		inc: asBool(search.inc),
		from: asDate(search.from),
		to: asDate(search.to),
		amin: asNumber(search.amin, 0, 100),
		amax: asNumber(search.amax, 0, 100),
		smin: asNumber(search.smin, 0),
		smax: asNumber(search.smax, 0),
		rank: normalizeRank(
			typeof search.rank === "string" ? search.rank : undefined,
		),
		fc: asBool(search.fc),
		bmin: asNumber(search.bmin, 0),
		bmax: asNumber(search.bmax, 0),
		recap: asBool(search.recap),
	};
}

/** Coerces a search value to a boolean, treating only "true"/true as true. */
function asBool(value: unknown): boolean {
	return value === "true" || value === true;
}

/** Parses a finite number within optional bounds, or `undefined` when absent/invalid. */
function asNumber(
	value: unknown,
	min?: number,
	max?: number,
): number | undefined {
	if (value == null || value === "") return undefined;
	const n = Number(value);
	if (!Number.isFinite(n)) return undefined;
	if (min != null && n < min) return min;
	if (max != null && n > max) return max;
	return n;
}

/** Accepts only `YYYY-MM-DD` date strings from the URL. */
function asDate(value: unknown): string | undefined {
	return typeof value === "string" && /^\d{4}-\d{2}-\d{2}$/.test(value)
		? value
		: undefined;
}

export function formatScore(score: number | string): string {
	return Number(score).toLocaleString("en-US");
}

export function formatAccuracy(acc: number | string): string {
	return `${(Number(acc) * 100).toFixed(1)}%`;
}

export function formatDuration(duration: string | null): string {
	if (!duration) return "—";
	const parts = duration.split(":");
	if (parts.length < 3) return "—";
	const h = Number(parts[0]);
	const m = Number(parts[1]);
	const s = Math.floor(Number(parts[2]));
	if (h > 0)
		return `${h}:${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
	return `${m}:${s.toString().padStart(2, "0")}`;
}

export function formatSongTimeMs(ms: number | string): string {
	const totalSeconds = Math.floor(Number(ms) / 1000);
	const m = Math.floor(totalSeconds / 60);
	const s = totalSeconds % 60;
	return `${m}:${s.toString().padStart(2, "0")}`;
}

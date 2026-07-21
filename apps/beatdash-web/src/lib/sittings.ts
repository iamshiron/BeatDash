import type { SittingSortBy } from "@/api/model";

/**
 * Ordering choices for the sessions (sittings) list. `value` is the URL-facing token
 * (kept terse for compact shareable links); `api` is the numeric {@link SittingSortBy}
 * the backend expects (0 Newest, 1 Oldest, 2 Most plays, 3 Longest).
 */
export const SITTING_SORT_OPTIONS = [
	{ value: "newest", label: "Newest first", api: 0 },
	{ value: "oldest", label: "Oldest first", api: 1 },
	{ value: "plays", label: "Most plays", api: 2 },
	{ value: "longest", label: "Longest", api: 3 },
] as const;

export type SittingSortValue = (typeof SITTING_SORT_OPTIONS)[number]["value"];

export const DEFAULT_SITTING_SORT: SittingSortValue = "newest";

const SORT_VALUES = new Set(SITTING_SORT_OPTIONS.map((o) => o.value));

/** Whitelists a raw sort token from the URL, falling back to the default when unknown. */
export function normalizeSittingSort(
	value: string | undefined,
): SittingSortValue {
	return value && SORT_VALUES.has(value as SittingSortValue)
		? (value as SittingSortValue)
		: DEFAULT_SITTING_SORT;
}

/** Maps a sort token to the numeric {@link SittingSortBy} the API expects. */
export function sittingSortToApi(value: SittingSortValue): SittingSortBy {
	return (
		SITTING_SORT_OPTIONS.find((o) => o.value === value)?.api ??
		SITTING_SORT_OPTIONS[0].api
	);
}

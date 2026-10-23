import type { GetApiMapsParams } from "@/api/model";

const DIFFICULTIES = [
	"easy",
	"normal",
	"hard",
	"expert",
	"expert-plus",
] as const;
const BOOLEAN_FILTERS = ["yes", "no"] as const;
const FETCH_STATUSES = [
	"pending",
	"skipped",
	"fetched",
	"not-found",
	"failed",
] as const;

export const MAP_SORT_OPTIONS = [
	{ value: "newest", label: "Recently added", api: 0 },
	{ value: "name", label: "Song name", api: 1 },
	{ value: "bpm", label: "BPM", api: 2 },
	{ value: "duration", label: "Duration", api: 3 },
	{ value: "nps", label: "Notes per second", api: 4 },
	{ value: "difficulty", label: "Difficulty rating", api: 5 },
	{ value: "pp", label: "Performance points", api: 6 },
	{ value: "downloads", label: "Downloads", api: 7 },
	{ value: "upvotes", label: "Upvotes", api: 8 },
	{ value: "score", label: "BeatSaver score", api: 9 },
	{ value: "uploaded", label: "BeatSaver upload date", api: 10 },
] as const;

export const DIFFICULTY_OPTIONS = [
	{ value: "easy", label: "Easy", api: 0 },
	{ value: "normal", label: "Normal", api: 1 },
	{ value: "hard", label: "Hard", api: 2 },
	{ value: "expert", label: "Expert", api: 3 },
	{ value: "expert-plus", label: "Expert+", api: 4 },
] as const;

export const FETCH_STATUS_OPTIONS = [
	{ value: "pending", label: "Pending", api: 0 },
	{ value: "skipped", label: "Skipped", api: 1 },
	{ value: "fetched", label: "Fetched", api: 2 },
	{ value: "not-found", label: "Not found", api: 3 },
	{ value: "failed", label: "Failed", api: 4 },
] as const;

export type PlayedFilter = "played" | "unplayed";
export type BooleanFilter = (typeof BOOLEAN_FILTERS)[number];
export type DifficultyFilter = (typeof DIFFICULTIES)[number];
export type FetchStatusFilter = (typeof FETCH_STATUSES)[number];
export type MapSortValue = (typeof MAP_SORT_OPTIONS)[number]["value"];
export type SortDirection = "asc" | "desc";

export type MapsSearch = {
	q?: string;
	p?: number;
	played?: PlayedFilter;
	liked?: boolean;
	characteristic?: string;
	difficulty?: DifficultyFilter;
	bpmMin?: number;
	bpmMax?: number;
	durationMin?: number;
	durationMax?: number;
	npsMin?: number;
	npsMax?: number;
	ratingMin?: number;
	ratingMax?: number;
	ppMin?: number;
	ppMax?: number;
	ranked?: BooleanFilter;
	automapper?: BooleanFilter;
	tag?: string;
	fetchStatus?: FetchStatusFilter;
	sort?: MapSortValue;
	direction?: SortDirection;
};

const DEFAULT_SORT: MapSortValue = "newest";
const DEFAULT_DIRECTION: SortDirection = "desc";

function optionalText(value: unknown): string | undefined {
	if (typeof value !== "string") return undefined;
	const normalized = value.trim();
	return normalized.length > 0 && normalized.length <= 100
		? normalized
		: undefined;
}

function optionalNumber(value: unknown): number | undefined {
	const number = typeof value === "number" ? value : Number(value);
	return Number.isFinite(number) && number >= 0 ? number : undefined;
}

function optionalEnum<T extends readonly string[]>(
	value: unknown,
	values: T,
): T[number] | undefined {
	return typeof value === "string" && values.includes(value)
		? value
		: undefined;
}

function normalizeRange(
	min: unknown,
	max: unknown,
): [number | undefined, number | undefined] {
	const normalizedMin = optionalNumber(min);
	const normalizedMax = optionalNumber(max);
	return normalizedMin !== undefined &&
		normalizedMax !== undefined &&
		normalizedMin > normalizedMax
		? [undefined, undefined]
		: [normalizedMin, normalizedMax];
}

/** Validates raw URL state into the maps catalog's shareable search state. */
export function normalizeMapsSearch(
	search: Record<string, unknown>,
): MapsSearch {
	const [bpmMin, bpmMax] = normalizeRange(search.bpmMin, search.bpmMax);
	const [durationMin, durationMax] = normalizeRange(
		search.durationMin,
		search.durationMax,
	);
	const [npsMin, npsMax] = normalizeRange(search.npsMin, search.npsMax);
	const [ratingMin, ratingMax] = normalizeRange(
		search.ratingMin,
		search.ratingMax,
	);
	const [ppMin, ppMax] = normalizeRange(search.ppMin, search.ppMax);

	return {
		q: optionalText(search.q),
		p: Math.max(1, Math.floor(optionalNumber(search.p) ?? 1)),
		played:
			search.played === "played" || search.played === "unplayed"
				? search.played
				: undefined,
		liked: search.liked === true || search.liked === "true" ? true : undefined,
		characteristic: optionalText(search.characteristic),
		difficulty: optionalEnum(search.difficulty, DIFFICULTIES),
		bpmMin,
		bpmMax,
		durationMin,
		durationMax,
		npsMin,
		npsMax,
		ratingMin,
		ratingMax,
		ppMin,
		ppMax,
		ranked: optionalEnum(search.ranked, BOOLEAN_FILTERS),
		automapper: optionalEnum(search.automapper, BOOLEAN_FILTERS),
		tag: optionalText(search.tag),
		fetchStatus: optionalEnum(search.fetchStatus, FETCH_STATUSES),
		sort: MAP_SORT_OPTIONS.find((option) => option.value === search.sort)
			?.value,
		direction:
			search.direction === "asc" || search.direction === "desc"
				? search.direction
				: undefined,
	};
}

/** Converts validated URL state to the generated maps endpoint parameters. */
export function mapsSearchToApi(search: MapsSearch): GetApiMapsParams {
	const difficulty = DIFFICULTY_OPTIONS.find(
		(option) => option.value === search.difficulty,
	)?.api;
	const fetchStatus = FETCH_STATUS_OPTIONS.find(
		(option) => option.value === search.fetchStatus,
	)?.api;
	const sortBy = MAP_SORT_OPTIONS.find(
		(option) => option.value === (search.sort ?? DEFAULT_SORT),
	)?.api;

	return {
		Page: search.p ?? 1,
		PageSize: 24,
		Search: search.q,
		Characteristic: search.characteristic,
		Difficulty: difficulty,
		MinBpm: search.bpmMin,
		MaxBpm: search.bpmMax,
		MinDurationSeconds: search.durationMin,
		MaxDurationSeconds: search.durationMax,
		MinNps: search.npsMin,
		MaxNps: search.npsMax,
		MinDifficulty: search.ratingMin,
		MaxDifficulty: search.ratingMax,
		MinPp: search.ppMin,
		MaxPp: search.ppMax,
		Ranked: toBoolean(search.ranked),
		Automapper: toBoolean(search.automapper),
		Tag: search.tag,
		FetchStatus: fetchStatus,
		Played:
			search.played === "played"
				? true
				: search.played === "unplayed"
					? false
					: undefined,
		Liked: search.liked ? true : undefined,
		SortBy: sortBy,
		SortDir: search.direction === "asc" ? 0 : 1,
	};
}

/** Returns the non-legacy filters currently affecting the catalog. */
export function getActiveMapFilters(search: MapsSearch): string[] {
	return [
		search.characteristic,
		search.difficulty,
		search.bpmMin,
		search.bpmMax,
		search.durationMin,
		search.durationMax,
		search.npsMin,
		search.npsMax,
		search.ratingMin,
		search.ratingMax,
		search.ppMin,
		search.ppMax,
		search.ranked,
		search.automapper,
		search.tag,
		search.fetchStatus,
	]
		.filter((value) => value !== undefined)
		.map(String);
}

function toBoolean(value: BooleanFilter | undefined): boolean | undefined {
	return value === "yes" ? true : value === "no" ? false : undefined;
}

export { DEFAULT_DIRECTION, DEFAULT_SORT };

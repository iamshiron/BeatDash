import type { GetApiSessionsParams } from "@/api/model";

export const DIFFICULTY_STYLES: Record<string, string> = {
	Easy: "border-emerald-500/25 bg-emerald-500/15 text-emerald-400",
	Normal: "border-sky-500/25 bg-sky-500/15 text-sky-400",
	Hard: "border-amber-500/25 bg-amber-500/15 text-amber-400",
	Expert: "border-rose-500/25 bg-rose-500/15 text-rose-400",
	ExpertPlus: "border-violet-500/25 bg-violet-500/15 text-violet-400",
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

export interface SessionSearchParams {
	page?: number;
	q?: string;
	difficulty?: string;
	sortBy?: string;
	sortDir?: string;
	includeAuto?: boolean;
}

export function toApiParams(search: SessionSearchParams): GetApiSessionsParams {
	return {
		Page: search.page ?? 1,
		PageSize: 25,
		Search: search.q?.trim() || undefined,
		Difficulty: search.difficulty
			? DIFFICULTY_TO_NUM[search.difficulty]
			: undefined,
		SortBy: SORT_TO_NUM[search.sortBy ?? "StartedAt"] ?? 0,
		SortDir: (search.sortDir ?? "Desc") === "Asc" ? 0 : 1,
		IncludeAuto: search.includeAuto ?? false,
	};
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

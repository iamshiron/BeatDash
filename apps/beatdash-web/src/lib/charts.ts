import type { ChartConfig } from "@shiron/ui/components/ui/chart";

/**
 * Sequential red→green ramp for an accuracy ratio in [0,1]. Shared by the
 * per-session and lifetime note grids and the cut-direction matrix so every
 * accuracy surface reads with one colour language.
 */
export function accuracyColor(ratio: number): string {
	const clamped = Math.max(0, Math.min(1, ratio));
	const hue = Math.round(clamped * 120);
	return `hsl(${hue} 70% 45%)`;
}

/** Gradient stops for the accuracy legend swatch. */
export const ACCURACY_GRADIENT =
	"linear-gradient(to right, hsl(0 70% 45%), hsl(60 70% 45%), hsl(120 70% 45%))";

/**
 * Beat Saber cut directions (0–8) mapped onto a 3×3 grid with the arrow rotation
 * that renders each one. Row/col are 0-indexed from the top-left; 8 (dot/any)
 * sits in the centre with no arrow.
 */
export interface CutDirectionMeta {
	label: string;
	row: number;
	col: number;
	/** Degrees to rotate an upward-pointing arrow; null for the dot. */
	rotation: number | null;
}

export const CUT_DIRECTIONS: Record<number, CutDirectionMeta> = {
	0: { label: "Up", row: 0, col: 1, rotation: 0 },
	1: { label: "Down", row: 2, col: 1, rotation: 180 },
	2: { label: "Left", row: 1, col: 0, rotation: -90 },
	3: { label: "Right", row: 1, col: 2, rotation: 90 },
	4: { label: "Up-left", row: 0, col: 0, rotation: -45 },
	5: { label: "Up-right", row: 0, col: 2, rotation: 45 },
	6: { label: "Down-left", row: 2, col: 0, rotation: -135 },
	7: { label: "Down-right", row: 2, col: 2, rotation: 135 },
	8: { label: "Dot", row: 1, col: 1, rotation: null },
};

/**
 * Fixed categorical palette for the five skill characteristics. One hue per
 * series, used verbatim in the skill-progression lines and any per-characteristic
 * chrome so the axes stay legible in light and dark.
 */
export const SKILL_AXES = [
	{ key: "stream", label: "Stream", color: "oklch(0.62 0.19 255)" },
	{ key: "tech", label: "Tech", color: "oklch(0.70 0.15 300)" },
	{ key: "speed", label: "Speed", color: "oklch(0.72 0.17 152)" },
	{ key: "jumps", label: "Jumps", color: "oklch(0.75 0.15 65)" },
	{ key: "gimmick", label: "Gimmick", color: "oklch(0.65 0.20 350)" },
] as const;

export const SKILL_CHART_CONFIG = Object.fromEntries(
	SKILL_AXES.map((a) => [a.key, { label: a.label, color: a.color }]),
) satisfies ChartConfig;

/** Hand identity colours (A/left = rose, B/right = blue). */
export const HAND_COLORS = {
	left: "oklch(0.62 0.24 27)",
	right: "oklch(0.62 0.19 255)",
} as const;

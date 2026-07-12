import type { NoteItemDto } from "@/api/model";
import {
	GridHeatmap,
	type GridHeatmapCell,
} from "@/components/analysis/GridHeatmap";

/**
 * Per-session note-placement accuracy heatmap. Aggregates this play's notes into
 * grid cells (center-accuracy score, 0–15, normalized to a ratio) and renders
 * them with the shared {@link GridHeatmap}.
 */
export function NoteGridHeatmap({ notes }: { notes: NoteItemDto[] }) {
	const acc = new Map<string, { count: number; totalScore: number }>();

	for (const note of notes) {
		const col = Number(note.lineIndex);
		const row = Number(note.noteLineLayer);
		const key = `${col}-${row}`;
		const existing = acc.get(key);
		const score = Number(note.centerDistanceScore);
		if (existing) {
			existing.count++;
			existing.totalScore += score;
		} else {
			acc.set(key, { count: 1, totalScore: score });
		}
	}

	const cells: GridHeatmapCell[] = [...acc.entries()].map(([key, v]) => {
		const [lineIndex, noteLineLayer] = key.split("-").map(Number);
		return {
			lineIndex,
			noteLineLayer,
			ratio: v.count > 0 ? v.totalScore / v.count / 15 : 0,
			count: v.count,
		};
	});

	return (
		<GridHeatmap cells={cells} caption="Avg accuracy score per grid position" />
	);
}

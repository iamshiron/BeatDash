import { ACCURACY_GRADIENT, accuracyColor } from "@/lib/charts";

const GRID_ROWS = [2, 1, 0] as const;
const GRID_COLS = [0, 1, 2, 3] as const;

/** A single note-grid cell keyed by column (lineIndex) and row (noteLineLayer). */
export interface GridHeatmapCell {
	lineIndex: number;
	noteLineLayer: number;
	/** Accuracy ratio in [0,1] driving the colour. */
	ratio: number;
	count: number;
}

/**
 * Presentational 4×3 note-placement heatmap. Cells are tinted by an accuracy
 * ratio via the shared ramp; empty positions render as a dashed placeholder.
 * Shared by the per-session and lifetime grids.
 */
export function GridHeatmap({
	cells,
	caption = "Accuracy by grid position",
}: {
	cells: GridHeatmapCell[];
	caption?: string;
}) {
	const byKey = new Map(
		cells.map((c) => [`${c.lineIndex}-${c.noteLineLayer}`, c]),
	);

	return (
		<div className="flex flex-col gap-1.5">
			{GRID_ROWS.map((row) => (
				<div key={`row-${row}`} className="flex gap-1.5">
					{GRID_COLS.map((col) => {
						const cell = byKey.get(`${col}-${row}`);
						if (!cell || cell.count === 0) {
							return (
								<div
									key={`cell-${col}-${row}`}
									className="flex h-16 flex-1 items-center justify-center rounded-lg border border-dashed border-border/30"
								>
									<span className="text-xs text-muted-foreground/30">—</span>
								</div>
							);
						}
						const color = accuracyColor(cell.ratio);
						return (
							<div
								key={`cell-${col}-${row}`}
								className="flex h-16 flex-1 flex-col items-center justify-center rounded-lg border border-border/20"
								style={{
									backgroundColor: `${color}25`,
									borderColor: `${color}50`,
								}}
							>
								<span
									className="font-mono text-sm font-semibold tabular-nums"
									style={{ color }}
								>
									{(cell.ratio * 100).toFixed(0)}%
								</span>
								<span className="text-xs text-muted-foreground">
									{cell.count.toLocaleString()}
								</span>
							</div>
						);
					})}
				</div>
			))}
			<div className="mt-1 flex items-center justify-between text-xs text-muted-foreground">
				<span>{caption}</span>
				<div className="flex items-center gap-1">
					<span>Low</span>
					<div
						className="h-2 w-16 rounded-full"
						style={{ background: ACCURACY_GRADIENT }}
					/>
					<span>High</span>
				</div>
			</div>
		</div>
	);
}

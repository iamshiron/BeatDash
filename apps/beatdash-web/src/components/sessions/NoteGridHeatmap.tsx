import { cn } from "@shiron/ui/lib/utils";
import type { NoteItemDto } from "@/api/model";

const GRID_ROWS = [2, 1, 0] as const;
const GRID_COLS = [0, 1, 2, 3] as const;

interface CellData {
	count: number;
	totalDistance: number;
}

function qualityColor(quality: number): string {
	const hue = Math.round(quality * 120);
	return `hsl(${hue} 70% 45%)`;
}

export function NoteGridHeatmap({ notes }: { notes: NoteItemDto[] }) {
	const cells = new Map<string, CellData>();

	for (const note of notes) {
		const col = Number(note.lineIndex);
		const row = Number(note.noteLineLayer);
		const key = `${col}-${row}`;
		const distance = Number(note.cutPointDistance);

		const existing = cells.get(key);
		if (existing) {
			existing.count++;
			existing.totalDistance += distance;
		} else {
			cells.set(key, {
				count: 1,
				totalDistance: distance,
			});
		}
	}

	const allDistances = [...cells.values()].map(
		(c) => c.totalDistance / c.count,
	);
	const minDist = Math.min(...allDistances);
	const maxDist = Math.max(...allDistances);
	const range = maxDist - minDist || 0.001;

	return (
		<div className="flex flex-col gap-1.5">
			{GRID_ROWS.map((row) => (
				<div key={`row-${row}`} className="flex gap-1.5">
					{GRID_COLS.map((col) => {
						const cell = cells.get(`${col}-${row}`);
						if (!cell) {
							return (
								<div
									key={`cell-${col}-${row}`}
									className="flex h-16 flex-1 items-center justify-center rounded-lg border border-dashed border-border/30"
								>
									<span className="text-xs text-muted-foreground/30">—</span>
								</div>
							);
						}
						const avgDist = cell.totalDistance / cell.count;
						const quality = 1 - (avgDist - minDist) / range;
						return (
							<div
								key={`cell-${col}-${row}`}
								className="flex h-16 flex-1 flex-col items-center justify-center rounded-lg border border-border/20"
								style={{
									backgroundColor: `${qualityColor(quality)}25`,
									borderColor: `${qualityColor(quality)}50`,
								}}
							>
								<span
									className="font-mono text-sm font-semibold tabular-nums"
									style={{ color: qualityColor(quality) }}
								>
									{avgDist.toFixed(3)}m
								</span>
								<span className="text-xs text-muted-foreground">
									{cell.count} {cell.count === 1 ? "note" : "notes"}
								</span>
							</div>
						);
					})}
				</div>
			))}
			<div className="mt-1 flex items-center justify-between text-xs text-muted-foreground">
				<span>Avg cut distance from center</span>
				<div className="flex items-center gap-1">
					<span>Near</span>
					<div
						className={cn("h-2 w-16 rounded-full")}
						style={{
							background:
								"linear-gradient(to right, hsl(120 70% 45%), hsl(60 70% 45%), hsl(0 70% 45%))",
						}}
					/>
					<span>Far</span>
				</div>
			</div>
		</div>
	);
}

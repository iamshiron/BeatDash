import { cn } from "@shiron/ui/lib/utils";
import type { NoteItemDto } from "@/api/model";

const GRID_ROWS = [2, 1, 0] as const;
const GRID_COLS = [0, 1, 2, 3] as const;

interface CellData {
	count: number;
	totalScore: number;
}

function accuracyColor(score15: number): string {
	const quality = score15 / 15;
	const hue = Math.round(quality * 120);
	return `hsl(${hue} 70% 45%)`;
}

export function NoteGridHeatmap({ notes }: { notes: NoteItemDto[] }) {
	const cells = new Map<string, CellData>();

	for (const note of notes) {
		const col = Number(note.lineIndex);
		const row = Number(note.noteLineLayer);
		const key = `${col}-${row}`;
		const score = Number(note.centerDistanceScore);

		const existing = cells.get(key);
		if (existing) {
			existing.count++;
			existing.totalScore += score;
		} else {
			cells.set(key, {
				count: 1,
				totalScore: score,
			});
		}
	}

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
						const avgScore = cell.totalScore / cell.count;
						return (
							<div
								key={`cell-${col}-${row}`}
								className="flex h-16 flex-1 flex-col items-center justify-center rounded-lg border border-border/20"
								style={{
									backgroundColor: `${accuracyColor(avgScore)}25`,
									borderColor: `${accuracyColor(avgScore)}50`,
								}}
							>
								<span
									className="font-mono text-sm font-semibold tabular-nums"
									style={{ color: accuracyColor(avgScore) }}
								>
									{avgScore.toFixed(1)}
									<span className="text-xs text-muted-foreground/60">/15</span>
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
				<span>Avg accuracy score per grid position</span>
				<div className="flex items-center gap-1">
					<span>Off-center</span>
					<div
						className={cn("h-2 w-16 rounded-full")}
						style={{
							background:
								"linear-gradient(to right, hsl(0 70% 45%), hsl(60 70% 45%), hsl(120 70% 45%))",
						}}
					/>
					<span>Centered</span>
				</div>
			</div>
		</div>
	);
}

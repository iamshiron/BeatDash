import {
	Card,
	CardContent,
	CardDescription,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import { AltArrowUp } from "@solar-icons/react";
import type { CutDirectionCellDto } from "@/api/model";
import { ACCURACY_GRADIENT, accuracyColor, CUT_DIRECTIONS } from "@/lib/charts";

const CELLS = [0, 1, 2] as const;

/** One hand's 3×3 directional accuracy grid. */
function HandGrid({
	title,
	cells,
}: {
	title: string;
	cells: CutDirectionCellDto[];
}) {
	const byDir = new Map(cells.map((c) => [Number(c.cutDirection), c]));

	return (
		<div className="flex flex-1 flex-col gap-2">
			<span className="text-center text-xs font-medium text-muted-foreground">
				{title}
			</span>
			<div className="flex flex-col gap-1.5">
				{CELLS.map((row) => (
					<div key={`r-${row}`} className="flex gap-1.5">
						{CELLS.map((col) => {
							const dir = Object.entries(CUT_DIRECTIONS).find(
								([, m]) => m.row === row && m.col === col,
							);
							const dirIndex = dir ? Number(dir[0]) : null;
							const meta = dir?.[1];
							const cell = dirIndex != null ? byDir.get(dirIndex) : undefined;

							if (!meta || !cell || Number(cell.count) === 0) {
								return (
									<div
										key={`c-${col}`}
										className="flex aspect-square flex-1 items-center justify-center rounded-lg border border-dashed border-border/30"
									/>
								);
							}
							const ratio = Number(cell.accuracy);
							const color = accuracyColor(ratio);
							return (
								<div
									key={`c-${col}`}
									className="flex aspect-square flex-1 flex-col items-center justify-center gap-0.5 rounded-lg border"
									style={{
										backgroundColor: `${color}22`,
										borderColor: `${color}55`,
									}}
									title={`${meta.label}: ${(ratio * 100).toFixed(1)}% acc · ${Number(cell.count).toLocaleString()} notes`}
								>
									{meta.rotation === null ? (
										<span
											className="size-2 rounded-full"
											style={{ backgroundColor: color }}
										/>
									) : (
										<AltArrowUp
											className="size-3.5"
											style={{
												color,
												transform: `rotate(${meta.rotation}deg)`,
											}}
										/>
									)}
									<span
										className="font-mono text-[11px] font-semibold tabular-nums"
										style={{ color }}
									>
										{(ratio * 100).toFixed(0)}
									</span>
								</div>
							);
						})}
					</div>
				))}
			</div>
		</div>
	);
}

/**
 * Career-wide cut accuracy by hand and swing direction — two 3×3 arrow grids
 * (left / right saber). Cells are tinted by average cut accuracy so persistent
 * weak angles stand out.
 */
export function CutDirectionMatrix({
	cells,
}: {
	cells: CutDirectionCellDto[];
}) {
	const left = cells.filter((c) => Number(c.hand) === 0);
	const right = cells.filter((c) => Number(c.hand) === 1);

	return (
		<Card>
			<CardHeader>
				<CardTitle>Cut direction accuracy</CardTitle>
				<CardDescription>
					Average accuracy per swing direction, per hand — lifetime.
				</CardDescription>
			</CardHeader>
			<CardContent className="flex flex-col gap-4">
				<div className="flex gap-6">
					<HandGrid title="Left" cells={left} />
					<HandGrid title="Right" cells={right} />
				</div>
				<div className="flex items-center justify-end gap-1 text-xs text-muted-foreground">
					<span>Low</span>
					<div
						className="h-2 w-16 rounded-full"
						style={{ background: ACCURACY_GRADIENT }}
					/>
					<span>High</span>
				</div>
			</CardContent>
		</Card>
	);
}

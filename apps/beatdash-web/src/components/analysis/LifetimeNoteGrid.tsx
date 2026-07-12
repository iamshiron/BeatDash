import {
	Card,
	CardContent,
	CardDescription,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import type { GridCellDto } from "@/api/model";
import {
	GridHeatmap,
	type GridHeatmapCell,
} from "@/components/analysis/GridHeatmap";

function toCells(cells: GridCellDto[], hand: number): GridHeatmapCell[] {
	return cells
		.filter((c) => Number(c.hand) === hand)
		.map((c) => ({
			lineIndex: Number(c.lineIndex),
			noteLineLayer: Number(c.noteLineLayer),
			ratio: Number(c.accuracy),
			count: Number(c.count),
		}));
}

/**
 * Career-wide note-placement accuracy heatmap, split by hand. Reuses the shared
 * {@link GridHeatmap} fed by the server-aggregated grid marginal.
 */
export function LifetimeNoteGrid({ cells }: { cells: GridCellDto[] }) {
	return (
		<Card>
			<CardHeader>
				<CardTitle>Note placement accuracy</CardTitle>
				<CardDescription>
					Average accuracy by grid position, per hand — lifetime.
				</CardDescription>
			</CardHeader>
			<CardContent className="grid gap-6 sm:grid-cols-2">
				<div className="flex flex-col gap-2">
					<span className="text-center text-xs font-medium text-muted-foreground">
						Left
					</span>
					<GridHeatmap cells={toCells(cells, 0)} caption="Left hand" />
				</div>
				<div className="flex flex-col gap-2">
					<span className="text-center text-xs font-medium text-muted-foreground">
						Right
					</span>
					<GridHeatmap cells={toCells(cells, 1)} caption="Right hand" />
				</div>
			</CardContent>
		</Card>
	);
}

import { cn } from "@shiron/ui/lib/utils";
import { useMemo } from "react";
import type { ActivityDayDto } from "@/api/model";

const WEEKS = 26;
const MONTH_LABELS = [
	"Jan",
	"Feb",
	"Mar",
	"Apr",
	"May",
	"Jun",
	"Jul",
	"Aug",
	"Sep",
	"Oct",
	"Nov",
	"Dec",
];

/** Local YYYY-MM-DD, matching the API's DateOnly serialization. */
function toKey(date: Date): string {
	const y = date.getFullYear();
	const m = `${date.getMonth() + 1}`.padStart(2, "0");
	const d = `${date.getDate()}`.padStart(2, "0");
	return `${y}-${m}-${d}`;
}

function levelFor(count: number): number {
	if (count <= 0) return 0;
	if (count < 3) return 1;
	if (count < 6) return 2;
	if (count < 10) return 3;
	return 4;
}

const LEVEL_CLASSES = [
	"bg-muted/50",
	"bg-primary/25",
	"bg-primary/45",
	"bg-primary/70",
	"bg-primary",
];

interface Cell {
	key: string;
	label: string;
	count: number;
	level: number;
	inFuture: boolean;
}

export function ActivityHeatmap({ activity }: { activity: ActivityDayDto[] }) {
	const { columns, monthMarkers, totalDays } = useMemo(() => {
		const counts = new Map<string, number>();
		for (const a of activity) counts.set(a.date, Number(a.count));

		const today = new Date();
		today.setHours(0, 0, 0, 0);
		// Sunday of the current week, then back up (WEEKS - 1) weeks for the grid start.
		const gridStart = new Date(today);
		gridStart.setDate(gridStart.getDate() - today.getDay() - (WEEKS - 1) * 7);

		const cols: Cell[][] = [];
		const markers: { col: number; label: string }[] = [];
		let lastMonth = -1;
		let activeDays = 0;

		for (let w = 0; w < WEEKS; w++) {
			const week: Cell[] = [];
			for (let d = 0; d < 7; d++) {
				const date = new Date(gridStart);
				date.setDate(date.getDate() + w * 7 + d);
				const key = toKey(date);
				const inFuture = date.getTime() > today.getTime();
				const count = inFuture ? 0 : (counts.get(key) ?? 0);
				if (count > 0) activeDays++;
				week.push({
					key,
					label: date.toLocaleDateString(undefined, {
						month: "short",
						day: "numeric",
						year: "numeric",
					}),
					count,
					level: levelFor(count),
					inFuture,
				});
				// Record a month label the first week a new month appears (top row).
				if (d === 0) {
					const month = date.getMonth();
					if (month !== lastMonth) {
						markers.push({ col: w, label: MONTH_LABELS[month] });
						lastMonth = month;
					}
				}
			}
			cols.push(week);
		}

		return { columns: cols, monthMarkers: markers, totalDays: activeDays };
	}, [activity]);

	return (
		<div className="w-full overflow-x-auto">
			<div className="inline-flex min-w-full flex-col gap-1">
				<div className="flex gap-[3px] pl-0 text-[10px] text-muted-foreground">
					{columns.map((week, colIndex) => {
						const marker = monthMarkers.find((m) => m.col === colIndex);
						return (
							<div
								key={`m-${week[0].key}`}
								className="w-[11px] shrink-0 tabular-nums"
							>
								{marker?.label ?? ""}
							</div>
						);
					})}
				</div>
				<div className="flex gap-[3px]">
					{columns.map((week) => (
						<div key={week[0].key} className="flex flex-col gap-[3px]">
							{week.map((cell) => (
								<div
									key={cell.key}
									title={
										cell.inFuture
											? undefined
											: `${cell.count} play${cell.count === 1 ? "" : "s"} · ${cell.label}`
									}
									className={cn(
										"size-[11px] shrink-0 rounded-[2px]",
										cell.inFuture
											? "bg-transparent"
											: LEVEL_CLASSES[cell.level],
									)}
								/>
							))}
						</div>
					))}
				</div>
				<div className="mt-1 flex items-center justify-between text-[10px] text-muted-foreground">
					<span>{totalDays} active days in the last 26 weeks</span>
					<div className="flex items-center gap-1">
						<span>Less</span>
						{LEVEL_CLASSES.map((c) => (
							<div key={c} className={cn("size-[11px] rounded-[2px]", c)} />
						))}
						<span>More</span>
					</div>
				</div>
			</div>
		</div>
	);
}

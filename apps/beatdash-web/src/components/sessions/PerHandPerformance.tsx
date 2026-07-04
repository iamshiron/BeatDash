import {
	Card,
	CardContent,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import {
	type ChartConfig,
	ChartContainer,
	ChartLegend,
	ChartLegendContent,
	ChartTooltip,
	ChartTooltipContent,
} from "@shiron/ui/components/ui/chart";
import { cn } from "@shiron/ui/lib/utils";
import { useMemo } from "react";
import {
	PolarAngleAxis,
	PolarGrid,
	PolarRadiusAxis,
	Radar,
	RadarChart,
} from "recharts";
import type { ComboBreakPointDto, NoteItemDto } from "@/api/model";

const handChartConfig = {
	left: { label: "Left Hand", color: "oklch(0.62 0.24 27)" },
	right: { label: "Right Hand", color: "oklch(0.62 0.19 255)" },
} satisfies ChartConfig;

interface HandStats {
	noteCount: number;
	comboBreaks: number;
	avgSpeed: number;
	avgAccuracy: number;
	avgPreSwing: number;
	avgPostSwing: number;
}

function computeHandStats(
	notes: NoteItemDto[],
	breakTimes: Set<number>,
): HandStats {
	if (notes.length === 0) {
		return {
			noteCount: 0,
			comboBreaks: 0,
			avgSpeed: 0,
			avgAccuracy: 0,
			avgPreSwing: 0,
			avgPostSwing: 0,
		};
	}
	let speed = 0;
	let accuracy = 0;
	let pre = 0;
	let post = 0;
	let breaks = 0;
	for (const n of notes) {
		speed += Number(n.saberSpeed);
		accuracy += Number(n.centerDistanceScore);
		pre += Number(n.preCutSwing);
		post += Number(n.postCutSwing);
		if (breakTimes.has(Number(n.songTimeMs))) breaks++;
	}
	const count = notes.length;
	return {
		noteCount: count,
		comboBreaks: breaks,
		avgSpeed: speed / count,
		avgAccuracy: accuracy / count,
		avgPreSwing: pre / count,
		avgPostSwing: post / count,
	};
}

export function PerHandPerformance({
	notes,
	comboBreaks,
}: {
	notes: NoteItemDto[];
	comboBreaks: ComboBreakPointDto[];
}) {
	const { leftStats, rightStats, radarData } = useMemo(() => {
		const breakTimes = new Set(comboBreaks.map((cb) => Number(cb.songTimeMs)));
		const leftNotes = notes.filter((n) => Number(n.colorType) === 0);
		const rightNotes = notes.filter((n) => Number(n.colorType) === 1);

		const left = computeHandStats(leftNotes, breakTimes);
		const right = computeHandStats(rightNotes, breakTimes);

		const clamp01 = (v: number) => Math.max(0, Math.min(1, v));

		const radarData = [
			{
				metric: "Speed",
				left: clamp01(left.avgSpeed / 20),
				right: clamp01(right.avgSpeed / 20),
			},
			{
				metric: "Accuracy",
				left: clamp01(left.avgAccuracy / 15),
				right: clamp01(right.avgAccuracy / 15),
			},
			{
				metric: "Pre-Swing",
				left: clamp01(left.avgPreSwing),
				right: clamp01(right.avgPreSwing),
			},
			{
				metric: "Post-Swing",
				left: clamp01(left.avgPostSwing),
				right: clamp01(right.avgPostSwing),
			},
			{
				metric: "Combo",
				left: left.noteCount > 0 ? 1 - left.comboBreaks / left.noteCount : 0,
				right:
					right.noteCount > 0 ? 1 - right.comboBreaks / right.noteCount : 0,
			},
		];

		return { leftStats: left, rightStats: right, radarData };
	}, [notes, comboBreaks]);

	if (leftStats.noteCount === 0 && rightStats.noteCount === 0) return null;

	return (
		<Card className="mb-4">
			<CardHeader>
				<CardTitle>Per-Hand Performance</CardTitle>
			</CardHeader>
			<CardContent>
				<div className="grid gap-4 sm:grid-cols-[200px_1fr]">
					<div className="flex flex-col gap-2">
						<HandStatsColumn
							title="Left"
							stats={leftStats}
							colorClass="text-rose-400"
						/>
						<HandStatsColumn
							title="Right"
							stats={rightStats}
							colorClass="text-sky-400"
						/>
					</div>
					<ChartContainer
						config={handChartConfig}
						className="mx-auto aspect-square w-full max-w-xs"
					>
						<RadarChart data={radarData} outerRadius="70%">
							<PolarGrid />
							<PolarAngleAxis dataKey="metric" />
							<PolarRadiusAxis domain={[0, 1]} tick={false} axisLine={false} />
							<Radar
								name="left"
								dataKey="left"
								stroke="var(--color-left)"
								fill="var(--color-left)"
								fillOpacity={0.15}
							/>
							<Radar
								name="right"
								dataKey="right"
								stroke="var(--color-right)"
								fill="var(--color-right)"
								fillOpacity={0.15}
							/>
							<ChartTooltip
								content={
									<ChartTooltipContent
										formatter={(value) =>
											`${(Number(value) * 100).toFixed(0)}%`
										}
									/>
								}
							/>
							<ChartLegend content={<ChartLegendContent />} />
						</RadarChart>
					</ChartContainer>
				</div>
			</CardContent>
		</Card>
	);
}

function HandStatsColumn({
	title,
	stats,
	colorClass,
}: {
	title: string;
	stats: HandStats;
	colorClass: string;
}) {
	return (
		<div className="rounded-lg border border-border bg-muted/30 p-3">
			<span className={cn("font-heading text-sm font-semibold", colorClass)}>
				{title}
			</span>
			<div className="mt-2 grid grid-cols-2 gap-y-1 text-xs">
				<span className="text-muted-foreground">Notes</span>
				<span className="text-right font-mono tabular-nums">
					{stats.noteCount}
				</span>
				<span className="text-muted-foreground">Breaks</span>
				<span className="text-right font-mono tabular-nums">
					{stats.comboBreaks}
				</span>
				<span className="text-muted-foreground">Avg Speed</span>
				<span className="text-right font-mono tabular-nums">
					{stats.avgSpeed.toFixed(1)} m/s
				</span>
				<span className="text-muted-foreground">Avg Accuracy</span>
				<span className="text-right font-mono tabular-nums">
					{stats.avgAccuracy.toFixed(1)}/15
				</span>
			</div>
		</div>
	);
}

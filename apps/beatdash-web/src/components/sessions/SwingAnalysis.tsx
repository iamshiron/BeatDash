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

const TIERS = [
	{
		key: "perfect",
		label: "Perfect",
		min: 115,
		color: "oklch(0.72 0.19 152)",
	},
	{
		key: "great",
		label: "Great",
		min: 100,
		color: "oklch(0.75 0.15 130)",
	},
	{
		key: "good",
		label: "Good",
		min: 80,
		color: "oklch(0.78 0.13 95)",
	},
	{
		key: "fair",
		label: "Fair",
		min: 50,
		color: "oklch(0.75 0.15 65)",
	},
	{
		key: "poor",
		label: "Poor",
		min: 1,
		color: "oklch(0.68 0.20 40)",
	},
	{
		key: "missed",
		label: "Missed",
		min: 0,
		color: "oklch(0.64 0.21 25)",
	},
] as const;

const radarConfig = {
	left: { label: "Left Hand", color: "oklch(0.62 0.24 27)" },
	right: { label: "Right Hand", color: "oklch(0.62 0.19 255)" },
} satisfies ChartConfig;

const RADAR_AXES = [
	{ key: "preSwing", label: "Pre-Swing", max: 70 },
	{ key: "accuracy", label: "Accuracy", max: 15 },
	{ key: "postSwing", label: "Post-Swing", max: 30 },
] as const;

function tierForScore(score: number): (typeof TIERS)[number] {
	for (const tier of TIERS) {
		if (score >= tier.min) return tier;
	}
	return TIERS[TIERS.length - 1];
}

interface TierCount {
	tier: (typeof TIERS)[number];
	count: number;
	pct: number;
}

interface HandStats {
	preSwing: number;
	accuracy: number;
	postSwing: number;
	count: number;
	breaks: number;
	avgSpeed: number;
}

function computeStats(
	notes: NoteItemDto[],
	breakTimes: Set<number>,
): HandStats {
	if (notes.length === 0) {
		return {
			preSwing: 0,
			accuracy: 0,
			postSwing: 0,
			count: 0,
			breaks: 0,
			avgSpeed: 0,
		};
	}
	let pre = 0;
	let acc = 0;
	let post = 0;
	let speed = 0;
	let breaks = 0;
	for (const n of notes) {
		pre += Number(n.beforeCutScore);
		acc += Number(n.centerDistanceScore);
		post += Number(n.afterCutScore);
		speed += Number(n.saberSpeed);
		if (breakTimes.has(Number(n.songTimeMs))) breaks++;
	}
	return {
		preSwing: pre / notes.length,
		accuracy: acc / notes.length,
		postSwing: post / notes.length,
		count: notes.length,
		breaks,
		avgSpeed: speed / notes.length,
	};
}

function computeTiers(notes: NoteItemDto[]): {
	tiers: TierCount[];
	total: number;
} {
	if (notes.length === 0) {
		return { tiers: [], total: 0 };
	}

	const counts = new Map<string, number>();
	for (const tier of TIERS) counts.set(tier.key, 0);

	for (const n of notes) {
		const score =
			Number(n.beforeCutScore) +
			Number(n.centerDistanceScore) +
			Number(n.afterCutScore);
		const tier = tierForScore(score);
		counts.set(tier.key, (counts.get(tier.key) ?? 0) + 1);
	}

	const total = notes.length;
	const tiers: TierCount[] = TIERS.filter(
		(t) => (counts.get(t.key) ?? 0) > 0,
	).map((tier) => ({
		tier,
		count: counts.get(tier.key) ?? 0,
		pct: ((counts.get(tier.key) ?? 0) / total) * 100,
	}));

	return { tiers, total };
}

function weakestAxis(left: HandStats, right: HandStats) {
	const leftNorm = {
		preSwing: left.preSwing / 70,
		accuracy: left.accuracy / 15,
		postSwing: left.postSwing / 30,
	};
	const rightNorm = {
		preSwing: right.preSwing / 70,
		accuracy: right.accuracy / 15,
		postSwing: right.postSwing / 30,
	};

	let weakest: (typeof RADAR_AXES)[number] = RADAR_AXES[0];
	let weakestVal = Infinity;
	for (const axis of RADAR_AXES) {
		const avg = (leftNorm[axis.key] + rightNorm[axis.key]) / 2;
		if (avg < weakestVal) {
			weakestVal = avg;
			weakest = axis;
		}
	}
	return { axis: weakest, value: weakestVal };
}

export function SwingAnalysis({
	notes,
	comboBreaks,
}: {
	notes: NoteItemDto[];
	comboBreaks: ComboBreakPointDto[];
}) {
	const {
		leftTiers,
		rightTiers,
		leftStats,
		rightStats,
		overall,
		radarData,
		weakest,
	} = useMemo(() => {
		const breakTimes = new Set(comboBreaks.map((cb) => Number(cb.songTimeMs)));
		const leftNotes = notes.filter((n) => Number(n.colorType) === 0);
		const rightNotes = notes.filter((n) => Number(n.colorType) === 1);

		const left = computeStats(leftNotes, breakTimes);
		const right = computeStats(rightNotes, breakTimes);
		const all = computeTiers(notes);

		const radarData = RADAR_AXES.map((axis) => ({
			metric: axis.label,
			left: left.count > 0 ? left[axis.key] / axis.max : 0,
			right: right.count > 0 ? right[axis.key] / axis.max : 0,
		}));

		return {
			leftTiers: computeTiers(leftNotes).tiers,
			rightTiers: computeTiers(rightNotes).tiers,
			leftStats: left,
			rightStats: right,
			overall: all,
			radarData,
			weakest: weakestAxis(left, right),
		};
	}, [notes, comboBreaks]);

	if (overall.total === 0) return null;

	return (
		<Card className="mb-4">
			<CardHeader>
				<CardTitle>Cut Quality</CardTitle>
			</CardHeader>
			<CardContent>
				<div className="space-y-4">
					<HandBar
						label="Left"
						tiers={leftTiers}
						stats={leftStats}
						colorClass="text-rose-400"
					/>
					<HandBar
						label="Right"
						tiers={rightTiers}
						stats={rightStats}
						colorClass="text-sky-400"
					/>
				</div>

				<div className="mt-4 flex flex-wrap gap-x-4 gap-y-1">
					{TIERS.filter((t) =>
						overall.tiers.some((tc) => tc.tier.key === t.key),
					).map((tier) => (
						<div key={tier.key} className="flex items-center gap-1.5">
							<div
								className="size-2.5 rounded-sm"
								style={{ backgroundColor: tier.color }}
							/>
							<span className="text-xs text-muted-foreground">
								{tier.label}
								{tier.min > 0 ? ` (${tier.min}+)` : ""}
							</span>
						</div>
					))}
				</div>

				<div className="mt-4 grid gap-4 sm:grid-cols-[1fr_160px]">
					<ChartContainer
						config={radarConfig}
						className="mx-auto aspect-square w-full max-w-[240px]"
					>
						<RadarChart data={radarData} outerRadius="68%">
							<PolarGrid />
							<PolarAngleAxis dataKey="metric" />
							<PolarRadiusAxis domain={[0, 1]} tick={false} axisLine={false} />
							<Radar
								name="left"
								dataKey="left"
								stroke="var(--color-left)"
								fill="var(--color-left)"
								fillOpacity={0.12}
							/>
							<Radar
								name="right"
								dataKey="right"
								stroke="var(--color-right)"
								fill="var(--color-right)"
								fillOpacity={0.12}
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

					<div className="flex flex-col justify-center gap-2">
						{RADAR_AXES.map((axis) => {
							const leftVal = leftStats[axis.key];
							const rightVal = rightStats[axis.key];
							return (
								<div key={axis.key}>
									<div className="flex items-baseline justify-between">
										<span
											className={cn(
												"text-xs",
												weakest.axis.key === axis.key
													? "font-semibold text-amber-400"
													: "text-muted-foreground",
											)}
										>
											{axis.label}
										</span>
										<span className="font-mono text-xs tabular-nums text-muted-foreground">
											{leftVal.toFixed(1)}/{axis.max}
										</span>
									</div>
									{rightStats.count > 0 && (
										<div className="text-right font-mono text-[10px] tabular-nums text-muted-foreground/60">
											{rightVal.toFixed(1)}/{axis.max}
										</div>
									)}
								</div>
							);
						})}
						<div className="mt-1 rounded-md bg-amber-500/10 px-2.5 py-1.5">
							<span className="text-[11px] text-amber-400">
								Focus on {weakest.axis.label} —{" "}
								{(weakest.value * 100).toFixed(0)}%
							</span>
						</div>
					</div>
				</div>
			</CardContent>
		</Card>
	);
}

function HandBar({
	label,
	tiers,
	stats,
	colorClass,
}: {
	label: string;
	tiers: TierCount[];
	stats: HandStats;
	colorClass: string;
}) {
	return (
		<div>
			<div className="mb-1.5 flex items-center justify-between">
				<span className={cn("text-sm font-medium", colorClass)}>{label}</span>
				<span className="font-mono text-xs tabular-nums text-muted-foreground">
					{stats.count} notes · {stats.breaks} breaks ·{" "}
					{stats.avgSpeed.toFixed(1)} m/s
				</span>
			</div>
			<div className="flex h-7 overflow-hidden rounded-md">
				{stats.count === 0 ? (
					<div className="flex h-full w-full items-center justify-center bg-muted text-xs text-muted-foreground">
						No notes
					</div>
				) : (
					tiers.map(({ tier, count, pct }) => (
						<div
							key={tier.key}
							className="flex h-full items-center justify-center transition-all"
							style={{
								width: `${pct}%`,
								backgroundColor: tier.color,
							}}
							title={`${tier.label}: ${count} (${pct.toFixed(1)}%)`}
						>
							{pct >= 8 && (
								<span className="font-mono text-[10px] font-medium tabular-nums text-white/90">
									{pct >= 15 ? `${pct.toFixed(0)}%` : ""}
								</span>
							)}
						</div>
					))
				)}
			</div>
		</div>
	);
}

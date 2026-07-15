import {
	Card,
	CardContent,
	CardDescription,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import {
	ChartContainer,
	ChartTooltip,
	ChartTooltipContent,
} from "@shiron/ui/components/ui/chart";
import { cn } from "@shiron/ui/lib/utils";
import { Dumbbell, MedalStar, Stopwatch, Target } from "@solar-icons/react";
import { formatDistanceToNow } from "date-fns";
import { Area, AreaChart, XAxis, YAxis } from "recharts";
import type { SessionSummaryDto } from "@/api/model";
import { useGetApiSessionsLatestSummary } from "@/api/sessions/sessions";
import { SessionTimeline } from "@/components/sessions/SessionTimeline";
import { formatAccuracy, RANK_STYLES } from "@/lib/sessions";

const sparkConfig = {
	accuracy: { label: "Accuracy", color: "oklch(0.62 0.19 255)" },
} as const;

function formatPlayTime(ms: number): string {
	const totalMinutes = Math.round(ms / 60000);
	const hours = Math.floor(totalMinutes / 60);
	const minutes = totalMinutes % 60;
	return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`;
}

function Metric({
	icon,
	label,
	value,
}: {
	icon?: React.ReactNode;
	label: string;
	value: string;
}) {
	return (
		<div className="flex flex-col gap-0.5 rounded-lg border border-border/40 bg-card/50 p-3">
			<span className="flex items-center gap-1 text-[11px] text-muted-foreground">
				{icon}
				{label}
			</span>
			<span className="font-heading text-lg font-bold tabular-nums">
				{value}
			</span>
		</div>
	);
}

/**
 * Overview of the player's most recent sitting (a cluster of plays). Renders
 * nothing until there's a session to summarize.
 */
export function SessionSummary({
	title = "Latest session",
	summary: summaryProp,
}: {
	title?: string;
	/** Provide directly to skip the fetch (e.g. when already loaded). */
	summary?: SessionSummaryDto | null;
}) {
	const query = useGetApiSessionsLatestSummary({
		query: { enabled: summaryProp === undefined },
	});
	const summary =
		summaryProp !== undefined
			? summaryProp
			: query.data?.status === 200
				? query.data.data
				: null;

	if (!summary || Number(summary.playCount) === 0) return null;

	const spark = summary.plays.map((p, i) => ({
		i: i + 1,
		accuracy: Number(p.results?.accuracy ?? 0) * 100,
	}));
	const travelKm = Number(summary.totalSaberTravel) / 1000;

	return (
		<Card>
			<CardHeader>
				<CardTitle>{title}</CardTitle>
				<CardDescription>
					{Number(summary.playCount)} plays ·{" "}
					{formatDistanceToNow(new Date(summary.endedAt), { addSuffix: true })}
				</CardDescription>
			</CardHeader>
			<CardContent className="flex flex-col gap-4">
				<div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
					<Metric
						icon={<Stopwatch className="size-3.5" />}
						label="Active time"
						value={formatPlayTime(Number(summary.totalPlayTimeMs))}
					/>
					<Metric
						icon={<Target className="size-3.5" />}
						label="Avg accuracy"
						value={formatAccuracy(Number(summary.avgAccuracy))}
					/>
					<Metric
						icon={<MedalStar className="size-3.5" />}
						label="Personal bests"
						value={`${Number(summary.personalBests)}`}
					/>
					<Metric
						icon={<Dumbbell className="size-3.5" />}
						label="Saber distance"
						value={travelKm >= 0.05 ? `${travelKm.toFixed(2)} km` : "—"}
					/>
				</div>

				{spark.length >= 2 && (
					<div className="flex flex-col gap-1">
						<span className="text-xs text-muted-foreground">
							Accuracy across the session
						</span>
						<ChartContainer config={sparkConfig} className="h-24 w-full">
							<AreaChart
								data={spark}
								margin={{ left: 4, right: 4, top: 4, bottom: 0 }}
							>
								<defs>
									<linearGradient id="sessionSpark" x1="0" y1="0" x2="0" y2="1">
										<stop
											offset="0%"
											stopColor="var(--color-accuracy)"
											stopOpacity={0.3}
										/>
										<stop
											offset="100%"
											stopColor="var(--color-accuracy)"
											stopOpacity={0.03}
										/>
									</linearGradient>
								</defs>
								<XAxis dataKey="i" hide />
								<YAxis domain={["dataMin - 2", "dataMax + 2"]} hide />
								<ChartTooltip
									content={
										<ChartTooltipContent
											labelFormatter={(_, p) => {
												const i = p?.[0]?.payload?.i;
												return i != null ? `Play #${i}` : "";
											}}
											formatter={(value) => (
												<span className="font-mono tabular-nums">
													{Number(value).toFixed(1)}%
												</span>
											)}
										/>
									}
								/>
								<Area
									dataKey="accuracy"
									stroke="var(--color-accuracy)"
									strokeWidth={2}
									fill="url(#sessionSpark)"
									type="monotone"
								/>
							</AreaChart>
						</ChartContainer>
					</div>
				)}

				<div className="flex flex-wrap gap-2">
					{summary.rankDistribution.map((r) => (
						<div
							key={r.rank}
							className="flex items-center gap-1.5 rounded-lg border border-border bg-muted/30 px-2.5 py-1"
						>
							<span
								className={cn(
									"font-heading text-sm font-bold",
									RANK_STYLES[r.rank] ?? "text-muted-foreground",
								)}
							>
								{r.rank}
							</span>
							<span className="font-mono text-xs tabular-nums text-muted-foreground">
								{Number(r.count)}
							</span>
						</div>
					))}
				</div>

				<SessionTimeline plays={summary.plays} />
			</CardContent>
		</Card>
	);
}

import { Badge } from "@shiron/ui/components/ui/badge";
import { Button } from "@shiron/ui/components/ui/button";
import {
	Card,
	CardContent,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import {
	type ChartConfig,
	ChartContainer,
	ChartTooltip,
	ChartTooltipContent,
} from "@shiron/ui/components/ui/chart";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import { cn } from "@shiron/ui/lib/utils";
import {
	ArrowLeftIcon,
	ArrowRightUpIcon,
	MusicNotesIcon,
} from "@solar-icons/react/dynamic";
import { createFileRoute, Link, redirect } from "@tanstack/react-router";
import { formatDistanceToNow } from "date-fns";
import { useMemo, useState } from "react";
import {
	Area,
	Bar,
	BarChart,
	CartesianGrid,
	ComposedChart,
	Line,
	XAxis,
	YAxis,
} from "recharts";
import { getGetApiMapsMapIdCoverUrl } from "@/api/maps/maps";
import {
	useGetApiSessionsId,
	useGetApiSessionsIdNotes,
	useGetApiSessionsIdRecap,
	useGetApiSessionsIdTimeline,
	useGetApiSessionsIdTop,
} from "@/api/sessions/sessions";
import { AppShell } from "@/components/layout/AppShell";
import { MotionSummary } from "@/components/sessions/MotionSummary";
import { NoteGridHeatmap } from "@/components/sessions/NoteGridHeatmap";
import { PerHandPerformance } from "@/components/sessions/PerHandPerformance";
import { ScoreBreakdown } from "@/components/sessions/ScoreBreakdown";
import { SessionRecap } from "@/components/sessions/SessionRecap";
import { SwingAnalysis } from "@/components/sessions/SwingAnalysis";
import { TopSessions } from "@/components/sessions/TopSessions";
import { WorkoutSummary } from "@/components/sessions/WorkoutSummary";
import {
	DIFFICULTY_STYLES,
	formatAccuracy,
	formatDuration,
	formatScore,
	formatSongTimeMs,
	parseSessionSearch,
	RANK_STYLES,
} from "@/lib/sessions";

const npsChartConfig = {
	nps: { label: "Notes/sec", color: "oklch(0.62 0.19 255)" },
} satisfies ChartConfig;

const comboMissesChartConfig = {
	combo: { label: "Combo", color: "oklch(0.72 0.17 152)" },
	misses: { label: "Misses", color: "oklch(0.64 0.21 25)" },
} satisfies ChartConfig;

import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export const Route = createFileRoute("/plays/$id")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	validateSearch: parseSessionSearch,
	component: SessionDetailPage,
});

function SessionDetailPage() {
	const { id } = Route.useParams();
	const listSearch = Route.useSearch();

	const detailQuery = useGetApiSessionsId(id);
	const timelineQuery = useGetApiSessionsIdTimeline(id);
	const notesQuery = useGetApiSessionsIdNotes(id);
	const topQuery = useGetApiSessionsIdTop(id);
	const showRecap = listSearch.recap === true;
	const recapQuery = useGetApiSessionsIdRecap(id, {
		query: { enabled: showRecap },
	});

	const detail =
		detailQuery.data?.status === 200 ? detailQuery.data.data : null;
	const recap = recapQuery.data?.status === 200 ? recapQuery.data.data : null;
	const timeline =
		timelineQuery.data?.status === 200 ? timelineQuery.data.data : null;
	const notes = notesQuery.data?.status === 200 ? notesQuery.data.data : null;
	const topDifficulties =
		topQuery.data?.status === 200 ? topQuery.data.data : null;
	const beatmap = detail?.beatmap;
	useDocumentTitle(beatmap?.songName);
	const results = detail?.results;

	// This session is a personal best when it's the top-scoring play on its difficulty.
	const isPersonalBest =
		topDifficulties?.find((d) => d.isCurrent)?.sessions?.[0]?.id === id;

	const diffStyle = beatmap
		? (DIFFICULTY_STYLES[beatmap.difficultyRank] ??
			"border-border bg-muted text-muted-foreground")
		: "";

	const [coverFailed, setCoverFailed] = useState(false);

	const comboMissesData = useMemo(() => {
		if (!notes || notes.length === 0) return [];
		type Event = { time: number; isBreak: boolean };
		const events: Event[] = [];
		for (const n of notes) {
			events.push({ time: Number(n.songTimeMs), isBreak: false });
		}
		for (const cb of timeline?.comboBreaks ?? []) {
			events.push({ time: Number(cb.songTimeMs), isBreak: true });
		}
		events.sort((a, b) => a.time - b.time);
		let combo = 0;
		let missCount = 0;
		return events.map((e) => {
			if (e.isBreak) {
				combo = 0;
				missCount++;
			} else {
				combo++;
			}
			return {
				time: Math.floor(e.time / 1000),
				combo,
				misses: missCount,
			};
		});
	}, [notes, timeline]);

	const npsData = useMemo(() => {
		if (!notes || notes.length === 0) return [];
		const binSize = 3;
		const songEnd = beatmap
			? Math.floor(Number(beatmap.durationMs) / 1000)
			: Math.ceil(Number(notes[notes.length - 1].songTimeMs) / 1000);
		const numBins = Math.ceil((songEnd + 1) / binSize);
		const bins = new Map<number, number>();
		for (const note of notes) {
			const idx = Math.floor(Number(note.songTimeMs) / 1000 / binSize);
			bins.set(idx, (bins.get(idx) ?? 0) + 1);
		}
		return Array.from({ length: numBins }, (_, i) => ({
			time: i * binSize,
			nps: Math.round(((bins.get(i) ?? 0) / binSize) * 10) / 10,
		}));
	}, [notes, beatmap]);

	return (
		<AppShell wide>
			<Button variant="ghost" size="sm" asChild className="mb-4 -ml-2 w-fit">
				<Link to="/plays" search={listSearch}>
					<ArrowLeftIcon className="size-4" />
					Plays
				</Link>
			</Button>

			{showRecap && recap && <SessionRecap recap={recap} />}

			{detailQuery.isLoading && <Skeleton className="h-32 rounded-xl" />}

			{detail && (
				<Card className="mb-4 py-0">
					<CardContent className="flex items-center gap-4 p-3">
						<div className="flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-md bg-gradient-to-br from-primary/40 to-[oklch(0.62_0.19_255)]/40">
							{beatmap && !coverFailed ? (
								<img
									src={getGetApiMapsMapIdCoverUrl(beatmap.id)}
									alt={beatmap.songName}
									loading="lazy"
									onError={() => setCoverFailed(true)}
									className="size-full object-cover"
								/>
							) : (
								<MusicNotesIcon className="size-6 text-muted-foreground/60" />
							)}
						</div>
						<div className="min-w-0 flex-1">
							<div className="flex items-center gap-2">
								{beatmap ? (
									<Link
										to="/maps/$id"
										params={{ id: beatmap.id }}
										className="group flex min-w-0 items-center gap-1 decoration-muted-foreground/50 underline-offset-4 hover:underline"
									>
										<h1 className="truncate font-heading text-lg font-semibold tracking-tight">
											{beatmap.songName}
										</h1>
										<ArrowRightUpIcon className="size-4 shrink-0 text-muted-foreground transition-colors group-hover:text-foreground" />
									</Link>
								) : (
									<h1 className="truncate font-heading text-lg font-semibold tracking-tight">
										Unknown map
									</h1>
								)}
								{beatmap?.difficultyName && (
									<Badge
										variant="outline"
										className={cn("shrink-0", diffStyle)}
									>
										{beatmap.difficultyName}
									</Badge>
								)}
								{isPersonalBest && (
									<Badge className="shrink-0 border-amber-500/30 bg-amber-500/15 text-amber-400">
										Personal Best
									</Badge>
								)}
								{results?.fullCombo && (
									<Badge
										variant="secondary"
										className="shrink-0 text-amber-400"
									>
										FC
									</Badge>
								)}
								{detail.autoMode && (
									<Badge variant="secondary" className="shrink-0">
										Auto
									</Badge>
								)}
							</div>
							<p className="mt-1 text-xs text-muted-foreground">
								by {beatmap?.songAuthor} · mapped by {beatmap?.mapper}
							</p>
							<p className="mt-0.5 text-xs text-muted-foreground/60">
								{formatDistanceToNow(new Date(detail.startedAt), {
									addSuffix: true,
								})}
								{detail.duration && ` · ${formatDuration(detail.duration)}`}
							</p>
						</div>

						{results ? (
							<div className="flex shrink-0 flex-col items-stretch pl-4">
								<span className="self-end font-heading text-3xl font-bold tabular-nums">
									{formatScore(results.score)}
								</span>
								<div className="mt-1 flex w-full items-center justify-between">
									<span
										className={cn(
											"font-heading text-lg font-bold",
											RANK_STYLES[results.rank] ?? "text-muted-foreground",
										)}
									>
										{results.rank}
									</span>
									<span className="text-xs tabular-nums text-muted-foreground">
										{formatAccuracy(results.accuracy)}
									</span>
								</div>
							</div>
						) : (
							<Badge variant="secondary">In progress</Badge>
						)}
					</CardContent>
				</Card>
			)}

			{results && (
				<div className="mb-4 grid grid-cols-2 gap-2 sm:grid-cols-4">
					<StatCard label="Max Combo" value={`${Number(results.maxCombo)}x`} />
					<StatCard label="Good Cuts" value={formatScore(results.goodCuts)} />
					<StatCard label="Bad Cuts" value={formatScore(results.badCuts)} />
					<StatCard label="Misses" value={formatScore(results.misses)} />
				</div>
			)}

			{results && notes && notes.length > 0 && <ScoreBreakdown notes={notes} />}

			{results && notes && notes.length > 0 && (
				<Card className="mb-4">
					<CardHeader>
						<CardTitle>Notes Per Second</CardTitle>
					</CardHeader>
					<CardContent>
						{notesQuery.isLoading && (
							<Skeleton className="h-32 w-full rounded-lg" />
						)}
						{npsData.length > 0 && (
							<ChartContainer config={npsChartConfig} className="h-32 w-full">
								<BarChart
									data={npsData}
									margin={{ left: 4, right: 4, top: 4, bottom: 4 }}
								>
									<CartesianGrid strokeDasharray="3 3" vertical={false} />
									<XAxis
										dataKey="time"
										tickFormatter={(v: number) => formatSongTimeMs(v * 1000)}
										tickLine={false}
										axisLine={false}
										tickMargin={8}
									/>
									<YAxis tickLine={false} axisLine={false} width={28} />
									<ChartTooltip
										content={
											<ChartTooltipContent
												labelFormatter={(_, payload) => {
													const time = payload?.[0]?.payload?.time;
													return time != null
														? formatSongTimeMs(time * 1000)
														: "";
												}}
											/>
										}
									/>
									<Bar
										dataKey="nps"
										fill="oklch(0.62 0.19 255)"
										radius={[2, 2, 0, 0]}
									/>
								</BarChart>
							</ChartContainer>
						)}
						{npsData.length === 0 && !notesQuery.isLoading && (
							<p className="py-8 text-center text-xs text-muted-foreground">
								No note data available
							</p>
						)}
					</CardContent>
				</Card>
			)}

			{results && (
				<Card className="mb-4">
					<CardHeader>
						<CardTitle>Combo & Misses</CardTitle>
					</CardHeader>
					<CardContent>
						{notesQuery.isLoading && (
							<Skeleton className="h-40 w-full rounded-lg" />
						)}
						{comboMissesData.length > 0 && (
							<ChartContainer
								config={comboMissesChartConfig}
								className="h-40 w-full"
							>
								<ComposedChart
									data={comboMissesData}
									margin={{ left: 4, right: 4, top: 4, bottom: 4 }}
								>
									<defs>
										<linearGradient id="comboFill" x1="0" y1="0" x2="0" y2="1">
											<stop
												offset="0%"
												stopColor="var(--color-combo)"
												stopOpacity={0.3}
											/>
											<stop
												offset="100%"
												stopColor="var(--color-combo)"
												stopOpacity={0.03}
											/>
										</linearGradient>
									</defs>
									<CartesianGrid strokeDasharray="3 3" vertical={false} />
									<XAxis
										dataKey="time"
										tickFormatter={(v: number) => formatSongTimeMs(v * 1000)}
										tickLine={false}
										axisLine={false}
										tickMargin={8}
									/>
									<YAxis
										yAxisId="combo"
										tickLine={false}
										axisLine={false}
										width={36}
									/>
									<YAxis
										yAxisId="misses"
										orientation="right"
										tickLine={false}
										axisLine={false}
										width={36}
										allowDecimals={false}
									/>
									<ChartTooltip
										content={
											<ChartTooltipContent
												labelFormatter={(_, payload) => {
													const time = payload?.[0]?.payload?.time;
													return time != null
														? formatSongTimeMs(time * 1000)
														: "";
												}}
											/>
										}
									/>
									<Area
										yAxisId="combo"
										dataKey="combo"
										stroke="var(--color-combo)"
										strokeWidth={2}
										fill="url(#comboFill)"
										type="linear"
									/>
									<Line
										yAxisId="misses"
										dataKey="misses"
										stroke="var(--color-misses)"
										strokeWidth={1.5}
										dot={false}
										type="linear"
									/>
								</ComposedChart>
							</ChartContainer>
						)}
						{comboMissesData.length === 0 && !notesQuery.isLoading && (
							<p className="py-8 text-center text-xs text-muted-foreground">
								No combo data available
							</p>
						)}
					</CardContent>
				</Card>
			)}

			{results && notes && notes.length > 0 && (
				<SwingAnalysis
					notes={notes}
					comboBreaks={timeline?.comboBreaks ?? []}
				/>
			)}

			{results && notes && notes.length > 0 && (
				<PerHandPerformance
					notes={notes}
					comboBreaks={timeline?.comboBreaks ?? []}
				/>
			)}

			{results && detail?.hasMotionSummary && <MotionSummary sessionId={id} />}

			{results && <WorkoutSummary sessionId={id} />}

			{results && (
				<Card className="mb-4">
					<CardHeader>
						<CardTitle>Note Grid</CardTitle>
					</CardHeader>
					<CardContent>
						{notesQuery.isLoading && (
							<Skeleton className="h-52 w-full rounded-lg" />
						)}
						{notes && notes.length > 0 && <NoteGridHeatmap notes={notes} />}
						{notes && notes.length === 0 && (
							<p className="py-8 text-center text-xs text-muted-foreground">
								No note data available
							</p>
						)}
					</CardContent>
				</Card>
			)}

			{results && topDifficulties && topDifficulties.length > 0 && (
				<TopSessions difficulties={topDifficulties} currentId={id} />
			)}

			{!results && detail && !detailQuery.isLoading && (
				<Card>
					<CardContent className="py-12 text-center">
						<p className="text-sm text-muted-foreground">
							This play is still in progress. Detailed stats will appear here
							once it completes.
						</p>
					</CardContent>
				</Card>
			)}
		</AppShell>
	);
}

function StatCard({ label, value }: { label: string; value: string }) {
	return (
		<Card size="sm">
			<CardContent className="flex flex-col items-center justify-center py-3">
				<span className="font-mono text-lg font-semibold tabular-nums">
					{value}
				</span>
				<span className="text-xs text-muted-foreground">{label}</span>
			</CardContent>
		</Card>
	);
}

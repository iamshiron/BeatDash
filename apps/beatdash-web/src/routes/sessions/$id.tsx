import { ArrowLeftIcon, MusicNotesSimpleIcon } from "@phosphor-icons/react";
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
import { createFileRoute, Link, redirect } from "@tanstack/react-router";
import { formatDistanceToNow } from "date-fns";
import { useMemo, useState } from "react";
import {
	Area,
	AreaChart,
	CartesianGrid,
	ComposedChart,
	Line,
	LineChart,
	XAxis,
	YAxis,
} from "recharts";
import { getGetApiMapsMapIdCoverUrl } from "@/api/maps/maps";
import {
	useGetApiSessionsId,
	useGetApiSessionsIdNotes,
	useGetApiSessionsIdTimeline,
} from "@/api/sessions/sessions";
import { AppShell } from "@/components/layout/AppShell";
import { NoteGridHeatmap } from "@/components/sessions/NoteGridHeatmap";
import { PerHandPerformance } from "@/components/sessions/PerHandPerformance";
import {
	DIFFICULTY_STYLES,
	formatAccuracy,
	formatDuration,
	formatScore,
	formatSongTimeMs,
	RANK_STYLES,
	type SessionSearchParams,
} from "@/lib/sessions";

const scoreChartConfig = {
	score: { label: "Score", color: "var(--chart-2)" },
} satisfies ChartConfig;

const energyChartConfig = {
	energy: { label: "Energy", color: "var(--chart-3)" },
} satisfies ChartConfig;

const comboMissesChartConfig = {
	combo: { label: "Combo", color: "oklch(0.72 0.17 152)" },
	misses: { label: "Misses", color: "oklch(0.64 0.21 25)" },
} satisfies ChartConfig;

export const Route = createFileRoute("/sessions/$id")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	validateSearch: (search: Record<string, unknown>): SessionSearchParams => ({
		page: typeof search.page === "number" ? search.page : undefined,
		q: typeof search.q === "string" ? search.q : undefined,
		difficulty:
			typeof search.difficulty === "string" ? search.difficulty : undefined,
		sortBy: typeof search.sortBy === "string" ? search.sortBy : undefined,
		sortDir: typeof search.sortDir === "string" ? search.sortDir : undefined,
		includeAuto: search.includeAuto === true,
	}),
	component: SessionDetailPage,
});

function SessionDetailPage() {
	const { id } = Route.useParams();
	const listSearch = Route.useSearch();

	const detailQuery = useGetApiSessionsId(id);
	const timelineQuery = useGetApiSessionsIdTimeline(id);
	const notesQuery = useGetApiSessionsIdNotes(id);

	const detail =
		detailQuery.data?.status === 200 ? detailQuery.data.data : null;
	const timeline =
		timelineQuery.data?.status === 200 ? timelineQuery.data.data : null;
	const notes = notesQuery.data?.status === 200 ? notesQuery.data.data : null;
	const beatmap = detail?.beatmap;
	const results = detail?.results;

	const diffStyle = beatmap
		? (DIFFICULTY_STYLES[beatmap.difficultyRank] ??
			"border-border bg-muted text-muted-foreground")
		: "";

	const [coverFailed, setCoverFailed] = useState(false);

	const comboMissesData = useMemo(() => {
		if (!notes || notes.length === 0) return [];
		const sorted = [...notes].sort(
			(a, b) => Number(a.songTimeMs) - Number(b.songTimeMs),
		);
		const breakTimes = new Set(
			(timeline?.comboBreaks ?? []).map((cb) => Number(cb.songTimeMs)),
		);
		let combo = 0;
		let misses = 0;
		return sorted.map((note) => {
			const time = Number(note.songTimeMs);
			if (breakTimes.has(time)) {
				combo = 0;
				misses++;
			} else {
				combo++;
			}
			return {
				time: Math.floor(time / 1000),
				combo,
				misses,
			};
		});
	}, [notes, timeline]);

	const energyData = useMemo(() => {
		if (!timeline || timeline.energy.length === 0) return [];
		const mapped = timeline.energy.map((p) => ({
			time: Math.floor(Number(p.songTimeMs) / 1000),
			energy: Number(p.energy),
		}));
		if (beatmap && mapped.length > 0) {
			const songEnd = Math.floor(Number(beatmap.durationMs) / 1000);
			const last = mapped[mapped.length - 1];
			if (last.time < songEnd) {
				mapped.push({ time: songEnd, energy: last.energy });
			}
		}
		return mapped;
	}, [timeline, beatmap]);

	return (
		<AppShell wide>
			<Button variant="ghost" size="sm" asChild className="mb-4 -ml-2 w-fit">
				<Link to="/sessions" search={listSearch}>
					<ArrowLeftIcon className="size-4" />
					Sessions
				</Link>
			</Button>

			{detailQuery.isLoading && <Skeleton className="h-32 rounded-xl" />}

			{detail && (
				<Card className="mb-4">
					<CardContent className="flex items-start gap-4 p-4">
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
								<MusicNotesSimpleIcon className="size-6 text-muted-foreground/60" />
							)}
						</div>
						<div className="min-w-0 flex-1">
							<div className="flex items-center gap-2">
								<h1 className="truncate font-heading text-lg font-semibold tracking-tight">
									{beatmap?.songName}
								</h1>
								{beatmap?.difficultyName && (
									<Badge
										variant="outline"
										className={cn("shrink-0", diffStyle)}
									>
										{beatmap.difficultyName}
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
							<div className="shrink-0 text-right">
								<span
									className={cn(
										"font-heading text-2xl font-bold",
										RANK_STYLES[results.rank] ?? "text-muted-foreground",
									)}
								>
									{results.rank}
								</span>
								<div className="font-mono text-sm tabular-nums">
									{formatScore(results.score)}
								</div>
								<div className="font-mono text-xs tabular-nums text-muted-foreground">
									{formatAccuracy(results.accuracy)}
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

			{results && (
				<Card className="mb-4">
					<CardHeader>
						<CardTitle>Score Timeline</CardTitle>
					</CardHeader>
					<CardContent>
						{timelineQuery.isLoading && (
							<Skeleton className="h-40 w-full rounded-lg" />
						)}
						{timeline && timeline.score.length > 0 && (
							<ChartContainer config={scoreChartConfig} className="h-40 w-full">
								<LineChart
									data={timeline.score.map((p) => ({
										time: Math.floor(Number(p.songTimeMs) / 1000),
										score: Number(p.score),
									}))}
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
									<YAxis
										tickFormatter={(v: number) =>
											v >= 1000 ? `${(v / 1000).toFixed(0)}k` : String(v)
										}
										tickLine={false}
										axisLine={false}
										width={36}
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
									<Line
										dataKey="score"
										stroke="var(--color-score)"
										strokeWidth={2}
										dot={false}
										type="monotone"
									/>
								</LineChart>
							</ChartContainer>
						)}
						{timeline && timeline.score.length === 0 && (
							<p className="py-8 text-center text-xs text-muted-foreground">
								No score timeline data available
							</p>
						)}
					</CardContent>
				</Card>
			)}

			{results && (
				<Card className="mb-4">
					<CardHeader>
						<CardTitle>Energy Timeline</CardTitle>
					</CardHeader>
					<CardContent>
						{timelineQuery.isLoading && (
							<Skeleton className="h-32 w-full rounded-lg" />
						)}
						{energyData.length > 0 && (
							<ChartContainer
								config={energyChartConfig}
								className="h-32 w-full"
							>
								<AreaChart
									data={energyData}
									margin={{ left: 4, right: 4, top: 4, bottom: 4 }}
								>
									<defs>
										<linearGradient id="energyFill" x1="0" y1="0" x2="0" y2="1">
											<stop
												offset="0%"
												stopColor="var(--color-energy)"
												stopOpacity={0.4}
											/>
											<stop
												offset="100%"
												stopColor="var(--color-energy)"
												stopOpacity={0.05}
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
										domain={[0, 1]}
										tickFormatter={(v: number) => `${Math.round(v * 100)}%`}
										tickLine={false}
										axisLine={false}
										width={36}
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
												formatter={(value) =>
													`${(Number(value) * 100).toFixed(1)}%`
												}
											/>
										}
									/>
									<Area
										dataKey="energy"
										stroke="var(--color-energy)"
										strokeWidth={2}
										fill="url(#energyFill)"
										type="monotone"
									/>
								</AreaChart>
							</ChartContainer>
						)}
						{energyData.length === 0 && !timelineQuery.isLoading && (
							<p className="py-8 text-center text-xs text-muted-foreground">
								No energy timeline data available
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
				<PerHandPerformance
					notes={notes}
					comboBreaks={timeline?.comboBreaks ?? []}
				/>
			)}

			{results && (
				<Card>
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

			{!results && detail && !detailQuery.isLoading && (
				<Card>
					<CardContent className="py-12 text-center">
						<p className="text-sm text-muted-foreground">
							This session is still in progress. Detailed stats will appear here
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

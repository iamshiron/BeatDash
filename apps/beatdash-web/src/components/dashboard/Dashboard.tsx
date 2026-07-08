import {
	ClockIcon,
	MusicNotesSimpleIcon,
	TargetIcon,
} from "@phosphor-icons/react";
import { Badge } from "@shiron/ui/components/ui/badge";
import { Button } from "@shiron/ui/components/ui/button";
import {
	Card,
	CardContent,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import { cn } from "@shiron/ui/lib/utils";
import { Link } from "@tanstack/react-router";
import { formatDistanceToNow } from "date-fns";
import { useState } from "react";
import { getGetApiMapsMapIdCoverUrl } from "@/api/maps/maps";
import type { MostPlayedMapDto, PlaySessionListItemDto } from "@/api/model";
import { useGetApiSessionsStats } from "@/api/sessions/sessions";
import { AccuracyTrend } from "@/components/dashboard/AccuracyTrend";
import { ActivityHeatmap } from "@/components/dashboard/ActivityHeatmap";
import { SkillProfile } from "@/components/dashboard/SkillProfile";
import { useAuth } from "@/contexts/auth";
import {
	DIFFICULTY_STYLES,
	formatAccuracy,
	formatScore,
	RANK_STYLES,
} from "@/lib/sessions";

const RANK_ORDER = ["SSS", "SS", "S", "A", "B", "C", "D", "E"];

function formatPlayTime(ms: number): string {
	const totalMinutes = Math.floor(ms / 60000);
	const hours = Math.floor(totalMinutes / 60);
	const minutes = totalMinutes % 60;
	if (hours > 0) return `${hours}h ${minutes}m`;
	return `${minutes}m`;
}

export function Dashboard() {
	const { user } = useAuth();
	const statsQuery = useGetApiSessionsStats();
	const stats = statsQuery.data?.status === 200 ? statsQuery.data.data : null;
	const name = user?.displayName || user?.userName || "player";

	return (
		<div className="flex flex-col gap-4">
			<div>
				<h1 className="font-heading text-2xl font-bold tracking-tight">
					Welcome back, <span className="text-primary">{name}</span>
				</h1>
				<p className="mt-1 text-sm text-muted-foreground">
					Here's how your Beat Saber sessions are shaping up.
				</p>
			</div>

			{statsQuery.isLoading && (
				<div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
					{[0, 1, 2, 3].map((i) => (
						<Skeleton key={i} className="h-20 rounded-xl" />
					))}
				</div>
			)}

			{stats && Number(stats.totalPlays) === 0 && <EmptyState />}

			{stats && Number(stats.totalPlays) > 0 && (
				<>
					<div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
						<StatTile
							icon={<MusicNotesSimpleIcon className="size-4" />}
							label="Plays"
							value={formatScore(Number(stats.totalPlays))}
						/>
						<StatTile
							icon={<ClockIcon className="size-4" />}
							label="Play time"
							value={formatPlayTime(Number(stats.totalPlayTimeMs))}
						/>
						<StatTile
							icon={<TargetIcon className="size-4" />}
							label="Avg accuracy"
							value={formatAccuracy(Number(stats.averageAccuracy))}
						/>
						<StatTile
							label="Full combos"
							value={`${formatScore(Number(stats.fullCombos))}`}
							sub={`${formatScore(Number(stats.uniqueMaps))} unique maps`}
						/>
					</div>

					<Card>
						<CardHeader>
							<CardTitle>Activity</CardTitle>
						</CardHeader>
						<CardContent>
							<ActivityHeatmap activity={stats.activity} />
						</CardContent>
					</Card>

					<AccuracyTrend />

					<SkillProfile />

					{stats.rankDistribution.length > 0 && (
						<Card>
							<CardHeader>
								<CardTitle>Rank distribution</CardTitle>
							</CardHeader>
							<CardContent>
								<div className="flex flex-wrap gap-2">
									{[...stats.rankDistribution]
										.sort(
											(a, b) =>
												RANK_ORDER.indexOf(a.rank) - RANK_ORDER.indexOf(b.rank),
										)
										.map((r) => (
											<div
												key={r.rank}
												className="flex items-center gap-2 rounded-lg border border-border bg-muted/30 px-3 py-1.5"
											>
												<span
													className={cn(
														"font-heading text-base font-bold",
														RANK_STYLES[r.rank] ?? "text-muted-foreground",
													)}
												>
													{r.rank}
												</span>
												<span className="font-mono text-sm tabular-nums text-muted-foreground">
													{formatScore(Number(r.count))}
												</span>
											</div>
										))}
								</div>
							</CardContent>
						</Card>
					)}

					<div className="grid gap-4 lg:grid-cols-2">
						<Card>
							<CardHeader>
								<CardTitle>Recent plays</CardTitle>
							</CardHeader>
							<CardContent className="flex flex-col gap-2">
								{stats.recentSessions.map((s) => (
									<SessionRow key={s.id} session={s} />
								))}
							</CardContent>
						</Card>

						<Card>
							<CardHeader>
								<CardTitle>Best accuracy</CardTitle>
							</CardHeader>
							<CardContent className="flex flex-col gap-2">
								{stats.topScores.map((s) => (
									<SessionRow key={s.id} session={s} />
								))}
							</CardContent>
						</Card>
					</div>

					{stats.mostPlayedMaps.length > 0 && (
						<Card>
							<CardHeader>
								<CardTitle>Most played</CardTitle>
							</CardHeader>
							<CardContent className="grid gap-2 sm:grid-cols-2">
								{stats.mostPlayedMaps.map((m) => (
									<MostPlayedRow key={m.beatmapId} map={m} />
								))}
							</CardContent>
						</Card>
					)}
				</>
			)}
		</div>
	);
}

function StatTile({
	icon,
	label,
	value,
	sub,
}: {
	icon?: React.ReactNode;
	label: string;
	value: string;
	sub?: string;
}) {
	return (
		<Card size="sm">
			<CardContent className="flex flex-col gap-1 py-4">
				<span className="flex items-center gap-1.5 text-xs text-muted-foreground">
					{icon}
					{label}
				</span>
				<span className="font-heading text-2xl font-bold tabular-nums">
					{value}
				</span>
				{sub && (
					<span className="text-[10px] text-muted-foreground">{sub}</span>
				)}
			</CardContent>
		</Card>
	);
}

function SessionRow({ session }: { session: PlaySessionListItemDto }) {
	const results = session.results;
	const [coverFailed, setCoverFailed] = useState(false);
	const diffStyle =
		DIFFICULTY_STYLES[session.difficultyRank] ??
		"border-border bg-muted text-muted-foreground";

	return (
		<Link
			to="/sessions/$id"
			params={{ id: session.id }}
			className="flex items-center gap-3 rounded-lg border border-border bg-card p-2 transition-colors hover:border-primary/40 hover:bg-accent/30"
		>
			<div className="size-10 shrink-0 overflow-hidden rounded-md bg-gradient-to-br from-primary/40 to-[oklch(0.62_0.19_255)]/40">
				{!coverFailed ? (
					<img
						src={getGetApiMapsMapIdCoverUrl(session.beatmapId)}
						alt={session.songName}
						loading="lazy"
						onError={() => setCoverFailed(true)}
						className="size-full object-cover"
					/>
				) : (
					<div className="flex size-full items-center justify-center">
						<MusicNotesSimpleIcon className="size-4 text-muted-foreground/40" />
					</div>
				)}
			</div>
			<div className="min-w-0 flex-1">
				<span className="block truncate font-heading text-sm font-semibold">
					{session.songName}
				</span>
				<div className="mt-0.5 flex items-center gap-1.5">
					<Badge
						variant="outline"
						className={cn("h-4 px-1 text-[9px]", diffStyle)}
					>
						{session.difficultyName}
					</Badge>
					<span className="truncate text-[10px] text-muted-foreground/60">
						{formatDistanceToNow(new Date(session.startedAt), {
							addSuffix: true,
						})}
					</span>
				</div>
			</div>
			{results && (
				<div className="flex shrink-0 items-center gap-2.5 border-l border-border/60 pl-2.5">
					<span
						className={cn(
							"font-heading text-base font-bold leading-none",
							RANK_STYLES[results.rank] ?? "text-muted-foreground",
						)}
					>
						{results.rank}
					</span>
					<div className="text-right">
						<div className="font-mono text-xs font-medium tabular-nums">
							{formatScore(results.score)}
						</div>
						<div className="font-mono text-[10px] tabular-nums text-muted-foreground">
							{formatAccuracy(results.accuracy)}
						</div>
					</div>
				</div>
			)}
		</Link>
	);
}

function MostPlayedRow({ map }: { map: MostPlayedMapDto }) {
	const [coverFailed, setCoverFailed] = useState(false);
	return (
		<Link
			to="/maps/$id"
			params={{ id: map.beatmapId }}
			className="flex items-center gap-3 rounded-lg border border-border bg-card p-2 transition-colors hover:border-primary/40 hover:bg-accent/30"
		>
			<div className="size-10 shrink-0 overflow-hidden rounded-md bg-gradient-to-br from-primary/40 to-[oklch(0.62_0.19_255)]/40">
				{!coverFailed ? (
					<img
						src={getGetApiMapsMapIdCoverUrl(map.beatmapId)}
						alt={map.songName}
						loading="lazy"
						onError={() => setCoverFailed(true)}
						className="size-full object-cover"
					/>
				) : (
					<div className="flex size-full items-center justify-center">
						<MusicNotesSimpleIcon className="size-4 text-muted-foreground/40" />
					</div>
				)}
			</div>
			<div className="min-w-0 flex-1">
				<span className="block truncate font-heading text-sm font-semibold">
					{map.songName}
				</span>
				<span className="block truncate text-[10px] text-muted-foreground/60">
					{map.songAuthor} · {map.mapper}
				</span>
			</div>
			<span className="shrink-0 font-mono text-xs tabular-nums text-muted-foreground">
				{formatScore(Number(map.playCount))}×
			</span>
		</Link>
	);
}

function EmptyState() {
	return (
		<Card>
			<CardContent className="flex flex-col items-center gap-3 py-12 text-center">
				<MusicNotesSimpleIcon className="size-8 text-muted-foreground/40" />
				<div>
					<p className="font-heading text-sm font-semibold">No plays yet</p>
					<p className="mt-1 text-xs text-muted-foreground">
						Pair a device and play a map — your stats will appear here.
					</p>
				</div>
				<Button asChild size="sm">
					<Link to="/devices">Pair a device</Link>
				</Button>
			</CardContent>
		</Card>
	);
}

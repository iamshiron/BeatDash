import { Button } from "@shiron/ui/components/ui/button";
import {
	Card,
	CardContent,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import { GlassPanel } from "@shiron/ui/components/ui/glass-panel";
import { GradientText } from "@shiron/ui/components/ui/gradient-text";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import { cn } from "@shiron/ui/lib/utils";
import { ClockCircle, MusicNotes, Target } from "@solar-icons/react";
import { Link } from "@tanstack/react-router";
import { useGetApiSessionsStats } from "@/api/sessions/sessions";
import { AccuracyTrend } from "@/components/dashboard/AccuracyTrend";
import { ActivityHeatmap } from "@/components/dashboard/ActivityHeatmap";
import { SkillProfile } from "@/components/dashboard/SkillProfile";
import { MostPlayedRow } from "@/components/profile/MostPlayedRow";
import { SessionRow } from "@/components/profile/SessionRow";
import { StatTile } from "@/components/profile/StatTile";
import { SessionSummary } from "@/components/sessions/SessionSummary";
import { useAuth } from "@/contexts/auth";
import { formatAccuracy, formatScore, RANK_STYLES } from "@/lib/sessions";

const RANK_ORDER = ["SSS", "SS", "S", "A", "B", "C", "D", "E"];

export function formatPlayTime(ms: number): string {
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
			<GlassPanel glow size="lg" className="flex flex-col gap-1">
				<h1 className="font-heading text-2xl font-bold tracking-tight">
					Welcome back, <GradientText>{name}</GradientText>
				</h1>
				<p className="text-sm text-muted-foreground">
					Here's how your Beat Saber plays are shaping up.
				</p>
			</GlassPanel>

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
							icon={<MusicNotes className="size-4" />}
							label="Plays"
							value={formatScore(Number(stats.totalPlays))}
						/>
						<StatTile
							icon={<ClockCircle className="size-4" />}
							label="Play time"
							value={formatPlayTime(Number(stats.totalPlayTimeMs))}
						/>
						<StatTile
							icon={<Target className="size-4" />}
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

					<SessionSummary title="Last session" />

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

function EmptyState() {
	return (
		<Card>
			<CardContent className="flex flex-col items-center gap-3 py-12 text-center">
				<MusicNotes className="size-8 text-muted-foreground/40" />
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

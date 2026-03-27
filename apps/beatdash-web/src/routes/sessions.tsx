import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { format, parseISO } from "date-fns";
import {
	CaretDown,
	MapPin,
	Trophy,
	Target,
	Timer,
	Note,
	Heart,
	Flame,
	Users,
	Waveform,
} from "@phosphor-icons/react";
import {
	Area,
	AreaChart,
	CartesianGrid,
	Line,
	LineChart,
	XAxis,
	YAxis,
} from "recharts";
import {
    Collapsible,
    CollapsibleContent,
    CollapsibleTrigger,
} from '@shiron/ui/components/ui/collapsible';
import { ScrollArea } from '@shiron/ui/components/ui/scroll-area';
import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
} from '@shiron/ui/components/ui/card';
import { Badge } from '@shiron/ui/components/ui/badge';
import { Separator } from '@shiron/ui/components/ui/separator';
import { Button } from '@shiron/ui/components/ui/button';
import { Skeleton } from '@shiron/ui/components/ui/skeleton';
import {
    ChartContainer,
    ChartTooltip,
    ChartTooltipContent,
    type ChartConfig,
} from '@shiron/ui/components/ui/chart';
import { cn } from '@shiron/ui/lib/utils';
import {
	useGetPlaySessions,
	useGetSessionDetail,
} from "@/api/play-session-endpoints/play-session-endpoints";
import type { PlaySessionSummaryDto } from "@/api/model";

function groupSessionsByDate(
	sessions: PlaySessionSummaryDto[],
): Map<string, PlaySessionSummaryDto[]> {
	const grouped = new Map<string, PlaySessionSummaryDto[]>();

	for (const session of sessions) {
		if (!session.startedAt) continue;
		const date = format(parseISO(session.startedAt), "yyyy-MM-dd");
		if (!grouped.has(date)) {
			grouped.set(date, []);
		}
		grouped.get(date)!.push(session);
	}

	return grouped;
}

function formatDuration(seconds: number): string {
	const mins = Math.floor(seconds / 60);
	const secs = Math.floor(seconds % 60);
	return `${mins}:${secs.toString().padStart(2, "0")}`;
}

function formatRelativeDate(dateStr: string): string {
	const date = parseISO(dateStr);
	const today = new Date();
	const yesterday = new Date(today);
	yesterday.setDate(yesterday.getDate() - 1);

	if (format(date, "yyyy-MM-dd") === format(today, "yyyy-MM-dd")) {
		return "Today";
	}
	if (format(date, "yyyy-MM-dd") === format(yesterday, "yyyy-MM-dd")) {
		return "Yesterday";
	}
	return format(date, "EEEE, MMMM d");
}

function getDifficultyVariant(
	difficulty: string,
): "default" | "secondary" | "destructive" | "outline" {
	switch (difficulty) {
		case "Expert":
		case "ExpertPlus":
			return "destructive";
		case "Hard":
			return "default";
		case "Normal":
			return "secondary";
		case "Easy":
			return "outline";
		default:
			return "secondary";
	}
}

function getRankColor(rank: string | null | undefined): string {
	if (!rank) return "text-muted-foreground";
	switch (rank) {
		case "SSS":
			return "text-yellow-500";
		case "SS":
			return "text-yellow-400";
		case "S":
			return "text-blue-400";
		case "A":
			return "text-green-400";
		case "B":
			return "text-yellow-300";
		case "C":
			return "text-orange-400";
		case "D":
		case "E":
			return "text-red-400";
		default:
			return "text-muted-foreground";
	}
}

const accuracyChartConfig = {
	accuracy: {
		label: "Accuracy",
		color: "var(--chart-1)",
	},
} satisfies ChartConfig;

const scoreChartConfig = {
	score: {
		label: "Score",
		color: "var(--chart-2)",
	},
} satisfies ChartConfig;

const healthChartConfig = {
	health: {
		label: "Health",
		color: "var(--chart-3)",
	},
} satisfies ChartConfig;

const comboChartConfig = {
	combo: {
		label: "Combo",
		color: "var(--chart-4)",
	},
	misses: {
		label: "Misses",
		color: "var(--destructive)",
	},
} satisfies ChartConfig;

function SessionCard({ session }: { session: PlaySessionSummaryDto }) {
	const [isOpen, setIsOpen] = useState(false);

	const { data: detailData, isLoading: detailLoading } = useGetSessionDetail(
		Number(session.id),
		{
			query: { enabled: isOpen },
		},
	);

	const durationSeconds = Number(session.durationSeconds) || 0;
	const finalScore = Number(session.finalScore) || 0;
	const finalAccuracy = Number(session.finalAccuracy) || 0;
	const finalMisses = Number(session.finalMisses) || 0;

	const detail = detailData?.data;
	const performance = detail?.performance;
	const maxCombo = Number(detail?.maxCombo) || 0;
	const avgHealth = Number(detail?.averageHealth) || 0;
	const snapshotCount = Number(detail?.snapshotCount) || 0;

	const accuracyData =
		performance?.accuracyOverTime?.points?.map((p) => ({
			time: Number(p.timeElapsed) || 0,
			accuracy: Number(p.value) || 0,
		})) ?? [];

	const scoreData =
		performance?.scoreOverTime?.points?.map((p) => ({
			time: Number(p.timeElapsed) || 0,
			score: Number(p.value) || 0,
		})) ?? [];

	const healthData =
		performance?.healthOverTime?.points?.map((p) => ({
			time: Number(p.timeElapsed) || 0,
			health: Number(p.value) || 0,
		})) ?? [];

	const comboData =
		performance?.comboOverTime?.points?.map((p) => ({
			time: Number(p.timeElapsed) || 0,
			combo: Number(p.combo) || 0,
			misses: Number(p.misses) || 0,
		})) ?? [];

	return (
		<Collapsible open={isOpen} onOpenChange={setIsOpen}>
			<Card
				className={cn(
					"transition-all overflow-hidden",
					isOpen && "ring-2 ring-primary/20",
				)}
			>
				<CollapsibleTrigger asChild>
					<CardContent className="p-0">
						<Button
							variant="ghost"
							className="w-full h-auto p-4 justify-start hover:bg-accent/50 rounded-none"
						>
							<div className="flex items-center gap-4 w-full">
								<div className="relative w-14 h-14 rounded-lg bg-muted flex items-center justify-center overflow-hidden shrink-0">
									{session.hasCoverImage ? (
										<img
											src={`/api/maps/${session.mapId}/cover`}
											alt={session.songName || "Map"}
											className="w-full h-full object-cover"
										/>
									) : (
										<MapPin className="w-6 h-6 text-muted-foreground" />
									)}
									{session.finalRank && (
										<div
											className={cn(
												"absolute bottom-0 right-0 text-[10px] font-bold px-1 bg-background/90 rounded-tl",
												getRankColor(session.finalRank),
											)}
										>
											{session.finalRank}
										</div>
									)}
								</div>
								<div className="flex-1 min-w-0 text-left">
									<div className="flex items-center gap-2 flex-wrap">
										<span className="font-medium truncate">
											{session.songName || "Unknown Song"}
										</span>
										{session.finalFullCombo && (
											<Badge
												variant="outline"
												className="text-[10px] px-1.5 bg-yellow-500/10 text-yellow-600 dark:text-yellow-400 border-yellow-500/20"
											>
												FC
											</Badge>
										)}
									</div>
									<div className="text-xs text-muted-foreground mt-0.5">
										{session.songAuthor && <span>{session.songAuthor}</span>}
										{session.mapper && (
											<span className="opacity-60"> • {session.mapper}</span>
										)}
									</div>
									<div className="flex items-center gap-4 text-xs text-muted-foreground mt-1">
										<span className="flex items-center gap-1">
											<Trophy className="w-3 h-3" />
											<span className="font-mono">
												{finalScore.toLocaleString()}
											</span>
										</span>
										<span className="flex items-center gap-1">
											<Target className="w-3 h-3" />
											<span
												className={cn(
													"font-mono",
													finalAccuracy >= 90
														? "text-green-500"
														: finalAccuracy >= 80
															? "text-yellow-500"
															: finalAccuracy >= 70
																? "text-orange-500"
																: "",
												)}
											>
												{finalAccuracy.toFixed(1)}%
											</span>
										</span>
										<span className="flex items-center gap-1">
											<Timer className="w-3 h-3" />
											{formatDuration(durationSeconds)}
										</span>
									</div>
								</div>
								<div className="flex items-center gap-2 shrink-0">
									{session.difficulty && (
										<Badge
											variant={getDifficultyVariant(session.difficulty)}
											className="text-[10px]"
										>
											{session.difficulty}
										</Badge>
									)}
									<CaretDown
										className={cn(
											"w-4 h-4 text-muted-foreground transition-transform duration-200",
											isOpen && "rotate-180",
										)}
									/>
								</div>
							</div>
						</Button>
					</CardContent>
				</CollapsibleTrigger>
				<CollapsibleContent>
					<CardContent className="pt-0 pb-4 px-4">
						<Separator className="mb-4" />

						{detailLoading ? (
							<div className="space-y-4">
								<div className="grid grid-cols-4 gap-3">
									{Array.from({ length: 8 }).map((_, i) => (
										<Skeleton key={i} className="h-16 rounded-lg" />
									))}
								</div>
							</div>
						) : (
							<div className="space-y-4">
								<div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
									<StatCard
										icon={<Trophy className="w-4 h-4" />}
										label="Final Score"
										value={finalScore.toLocaleString()}
									/>
									<StatCard
										icon={<Target className="w-4 h-4" />}
										label="Accuracy"
										value={`${finalAccuracy.toFixed(2)}%`}
										highlight
										highlightColor={
											finalAccuracy >= 90
												? "green"
												: finalAccuracy >= 80
													? "yellow"
													: undefined
										}
									/>
									<StatCard
										icon={<Flame className="w-4 h-4" />}
										label="Max Combo"
										value={maxCombo.toLocaleString()}
									/>
									<StatCard
										icon={<Heart className="w-4 h-4" />}
										label="Avg Health"
										value={`${(avgHealth * 100).toFixed(0)}%`}
										highlightColor={
											avgHealth >= 0.8
												? "green"
												: avgHealth >= 0.5
													? "yellow"
													: "red"
										}
									/>
									<StatCard
										icon={<Note className="w-4 h-4" />}
										label="Misses"
										value={finalMisses}
										variant={finalMisses > 0 ? "destructive" : "default"}
									/>
									<StatCard
										icon={<Waveform className="w-4 h-4" />}
										label="Map Type"
										value={session.mapType || "Standard"}
									/>
									<StatCard
										icon={<Timer className="w-4 h-4" />}
										label="Duration"
										value={formatDuration(durationSeconds)}
									/>
									<StatCard
										icon={<Users className="w-4 h-4" />}
										label="Data Points"
										value={snapshotCount}
									/>
								</div>

								{accuracyData.length > 0 && (
									<div className="grid grid-cols-1 lg:grid-cols-2 gap-4 mt-4">
										<Card>
											<CardHeader className="pb-2">
												<CardTitle className="text-sm">
													Accuracy Over Time
												</CardTitle>
											</CardHeader>
											<CardContent>
												<ChartContainer
													config={accuracyChartConfig}
													className="h-32 w-full"
												>
													<AreaChart data={accuracyData}>
														<CartesianGrid strokeDasharray="3 3" />
														<XAxis
															dataKey="time"
															tickFormatter={(v) => formatDuration(v)}
															tick={{
																fontSize: 10,
															}}
														/>
														<YAxis
															domain={[0, 100]}
															tick={{
																fontSize: 10,
															}}
															tickFormatter={(v) => `${v}%`}
														/>
														<ChartTooltip content={<ChartTooltipContent />} />
														<Area
															type="monotone"
															dataKey="accuracy"
															stroke="var(--color-accuracy)"
															fill="var(--color-accuracy)"
															fillOpacity={0.2}
														/>
													</AreaChart>
												</ChartContainer>
											</CardContent>
										</Card>

										<Card>
											<CardHeader className="pb-2">
												<CardTitle className="text-sm">
													Score Progress
												</CardTitle>
											</CardHeader>
											<CardContent>
												<ChartContainer
													config={scoreChartConfig}
													className="h-32 w-full"
												>
													<AreaChart data={scoreData}>
														<CartesianGrid strokeDasharray="3 3" />
														<XAxis
															dataKey="time"
															tickFormatter={(v) => formatDuration(v)}
															tick={{
																fontSize: 10,
															}}
														/>
														<YAxis
															tick={{
																fontSize: 10,
															}}
															tickFormatter={(v) =>
																v >= 1000 ? `${(v / 1000).toFixed(0)}k` : v
															}
														/>
														<ChartTooltip content={<ChartTooltipContent />} />
														<Area
															type="monotone"
															dataKey="score"
															stroke="var(--color-score)"
															fill="var(--color-score)"
															fillOpacity={0.2}
														/>
													</AreaChart>
												</ChartContainer>
											</CardContent>
										</Card>

										<Card>
											<CardHeader className="pb-2">
												<CardTitle className="text-sm">
													Health Over Time
												</CardTitle>
											</CardHeader>
											<CardContent>
												<ChartContainer
													config={healthChartConfig}
													className="h-32 w-full"
												>
													<AreaChart data={healthData}>
														<CartesianGrid strokeDasharray="3 3" />
														<XAxis
															dataKey="time"
															tickFormatter={(v) => formatDuration(v)}
															tick={{
																fontSize: 10,
															}}
														/>
														<YAxis
															domain={[0, 1]}
															tick={{
																fontSize: 10,
															}}
															tickFormatter={(v) => `${(v * 100).toFixed(0)}%`}
														/>
														<ChartTooltip content={<ChartTooltipContent />} />
														<Area
															type="monotone"
															dataKey="health"
															stroke="var(--color-health)"
															fill="var(--color-health)"
															fillOpacity={0.2}
														/>
													</AreaChart>
												</ChartContainer>
											</CardContent>
										</Card>

										<Card>
											<CardHeader className="pb-2">
												<CardTitle className="text-sm">
													Combo & Misses
												</CardTitle>
											</CardHeader>
											<CardContent>
												<ChartContainer
													config={comboChartConfig}
													className="h-32 w-full"
												>
													<LineChart data={comboData}>
														<CartesianGrid strokeDasharray="3 3" />
														<XAxis
															dataKey="time"
															tickFormatter={(v) => formatDuration(v)}
															tick={{
																fontSize: 10,
															}}
														/>
														<YAxis
															yAxisId="left"
															tick={{
																fontSize: 10,
															}}
														/>
														<YAxis
															yAxisId="right"
															orientation="right"
															tick={{
																fontSize: 10,
															}}
														/>
														<ChartTooltip content={<ChartTooltipContent />} />
														<Line
															yAxisId="left"
															type="monotone"
															dataKey="combo"
															stroke="var(--color-combo)"
															dot={false}
															strokeWidth={2}
														/>
														<Line
															yAxisId="right"
															type="monotone"
															dataKey="misses"
															stroke="var(--color-misses)"
															dot={false}
															strokeWidth={2}
														/>
													</LineChart>
												</ChartContainer>
											</CardContent>
										</Card>
									</div>
								)}

								{session.startedAt && (
									<div className="text-xs text-muted-foreground pt-2">
										Played at{" "}
										{format(parseISO(session.startedAt), "MMM d, yyyy h:mm a")}
									</div>
								)}
							</div>
						)}
					</CardContent>
				</CollapsibleContent>
			</Card>
		</Collapsible>
	);
}

function StatCard({
	icon,
	label,
	value,
	highlight,
	highlightColor,
	variant = "default",
}: {
	icon: React.ReactNode;
	label: string;
	value: string | number;
	highlight?: boolean;
	highlightColor?: "green" | "yellow" | "red";
	variant?: "default" | "destructive";
}) {
	const colorClasses = {
		green: "bg-green-500/10 text-green-600 dark:text-green-400",
		yellow: "bg-yellow-500/10 text-yellow-600 dark:text-yellow-400",
		red: "bg-red-500/10 text-red-600 dark:text-red-400",
	};

	return (
		<div className="bg-muted/50 rounded-lg p-3">
			<div className="flex items-center gap-2 mb-1">
				<div
					className={cn(
						"w-6 h-6 rounded flex items-center justify-center",
						variant === "destructive"
							? "bg-destructive/10 text-destructive"
							: highlightColor
								? colorClasses[highlightColor]
								: "bg-background",
						highlight && !highlightColor && "bg-primary/10 text-primary",
					)}
				>
					{icon}
				</div>
				<span className="text-[10px] text-muted-foreground">{label}</span>
			</div>
			<div
				className={cn(
					"text-lg font-semibold font-mono",
					variant === "destructive" && "text-destructive",
					highlightColor === "green" && "text-green-600 dark:text-green-400",
					highlightColor === "yellow" && "text-yellow-600 dark:text-yellow-400",
					highlightColor === "red" && "text-red-600 dark:text-red-400",
				)}
			>
				{typeof value === "number" ? value.toLocaleString() : value}
			</div>
		</div>
	);
}

function SessionSkeleton() {
	return (
		<Card>
			<CardContent className="p-4">
				<div className="flex items-center gap-4">
					<Skeleton className="w-14 h-14 rounded-lg" />
					<div className="flex-1 space-y-2">
						<Skeleton className="h-4 w-48" />
						<Skeleton className="h-3 w-32" />
						<Skeleton className="h-3 w-64" />
					</div>
					<Skeleton className="w-16 h-5 rounded-full" />
				</div>
			</CardContent>
		</Card>
	);
}

export const Route = createFileRoute("/sessions")({
	component: SessionsPage,
});

function SessionsPage() {
	const { data, isLoading, error } = useGetPlaySessions({
		pageSize: 100,
		sortDescending: true,
	});

	const sessions = data?.data?.items ?? [];
	const totalCount = Number(data?.data?.totalCount) ?? 0;
	const groupedSessions = groupSessionsByDate(sessions);
	const sortedDates = Array.from(groupedSessions.keys()).sort((a, b) =>
		b.localeCompare(a),
	);

	if (error) {
		return (
			<div className="space-y-6">
				<h1 className="text-2xl font-bold">Sessions</h1>
				<Card>
					<CardContent className="p-6 text-center text-muted-foreground">
						Failed to load sessions. Please try again later.
					</CardContent>
				</Card>
			</div>
		);
	}

	return (
		<div className="space-y-6">
			<div className="flex items-center justify-between">
				<div>
					<h1 className="text-2xl font-bold">Sessions</h1>
					<p className="text-sm text-muted-foreground">
						Your play history and performance
					</p>
				</div>
				<Badge variant="secondary" className="text-sm">
					{totalCount} plays
				</Badge>
			</div>

			<ScrollArea className="h-[calc(100vh-220px)]">
				<div className="space-y-6 pr-4">
					{isLoading ? (
						<div className="space-y-2">
							{Array.from({ length: 5 }).map((_, i) => (
								<SessionSkeleton key={i} />
							))}
						</div>
					) : sortedDates.length === 0 ? (
						<Card>
							<CardContent className="p-8 text-center">
								<div className="text-muted-foreground mb-2">
									No sessions found
								</div>
								<p className="text-sm text-muted-foreground/60">
									Start playing Beat Saber to see your history here!
								</p>
							</CardContent>
						</Card>
					) : (
						sortedDates.map((date) => (
							<div key={date}>
								<div className="sticky top-0 bg-background/95 backdrop-blur-sm z-10 py-2 mb-2 border-b">
									<div className="flex items-center justify-between">
										<h2 className="text-sm font-medium text-muted-foreground">
											{formatRelativeDate(date)}
										</h2>
										<div className="flex items-center gap-3 text-xs text-muted-foreground">
											<span>
												{groupedSessions.get(date)?.length ?? 0} plays
											</span>
											<span>
												Avg:{" "}
												{(
													(groupedSessions
														.get(date)
														?.reduce(
															(sum, s) => sum + (Number(s.finalAccuracy) || 0),
															0,
														) ?? 0) / (groupedSessions.get(date)?.length ?? 1)
												).toFixed(1)}
												%
											</span>
											<span>
												{formatDuration(
													groupedSessions
														.get(date)
														?.reduce(
															(sum, s) =>
																sum + (Number(s.durationSeconds) || 0),
															0,
														) ?? 0,
												)}
											</span>
										</div>
									</div>
								</div>
								<div className="space-y-2">
									{groupedSessions.get(date)?.map((session) => (
										<SessionCard
											key={session.id?.toString()}
											session={session}
										/>
									))}
								</div>
							</div>
						))
					)}
				</div>
			</ScrollArea>
		</div>
	);
}

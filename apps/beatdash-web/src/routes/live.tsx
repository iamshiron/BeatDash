import { Badge } from "@shiron/ui/components/ui/badge";
import { Button } from "@shiron/ui/components/ui/button";
import {
	Empty,
	EmptyContent,
	EmptyDescription,
	EmptyHeader,
	EmptyMedia,
	EmptyTitle,
} from "@shiron/ui/components/ui/empty";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import { cn } from "@shiron/ui/lib/utils";
import {
	Bolt,
	Dumbbell,
	Fire,
	HeartPulse,
	Monitor,
	MusicNote,
	MusicNotes,
	Pulse,
	Scale,
	Target,
	Widget,
} from "@solar-icons/react";
import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link, redirect } from "@tanstack/react-router";
import { useEffect, useRef, useState } from "react";
import { getGetApiDeviceQueryKey, useGetApiDevice } from "@/api/device/device";
import { getGetApiMapsMapIdCoverUrl } from "@/api/maps/maps";
import type { PersonalBestDto } from "@/api/model";
import { useGetApiSessionsPb } from "@/api/sessions/sessions";
import { AppShell } from "@/components/layout/AppShell";
import { SessionSummary } from "@/components/sessions/SessionSummary";
import {
	type LiveMapStartedEvent,
	type MapResults,
	type ScoreUpdateEvent,
	useRealtimeEvent,
} from "@/realtime";

export const Route = createFileRoute("/live")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	component: LivePage,
});

function LivePage() {
	const [currentMap, setCurrentMap] = useState<LiveMapStartedEvent | null>(
		null,
	);
	const [scoreUpdate, setScoreUpdate] = useState<ScoreUpdateEvent | null>(null);
	const [recap, setRecap] = useState<MapResults | null>(null);
	const [coverFailed, setCoverFailed] = useState(false);
	const { data, isLoading } = useGetApiDevice();
	const devices = data?.status === 200 ? data.data : [];
	const onlineDevice = devices.find((d) => d.session != null);

	const queryClient = useQueryClient();
	useRealtimeEvent("receiveDeviceStatus", (event) => {
		if (!event.isOnline) {
			setCurrentMap(null);
			setScoreUpdate(null);
			setRecap(null);
		}
		queryClient.invalidateQueries({
			queryKey: getGetApiDeviceQueryKey(),
		});
	});
	useRealtimeEvent("receiveLiveMapStarted", (event) => {
		setCurrentMap(event);
		setCoverFailed(false);
		setScoreUpdate(null);
		setRecap(null);
	});
	useRealtimeEvent("receiveScoreUpdate", (event) => {
		setScoreUpdate(event);
	});
	// On a completed play, surface a recap comparing the final score to the PB.
	useRealtimeEvent("receiveLiveMapStateChanged", (event) => {
		if (event.results && event.state === "Finished") {
			setRecap(event.results);
		}
	});

	// The user's best previous score on the current difficulty, shown as a target.
	const pbQuery = useGetApiSessionsPb(
		{
			mapId: currentMap?.mapId ?? "",
			difficulty: currentMap?.difficulty ?? "",
			characteristic: currentMap?.characteristic ?? "",
		},
		{ query: { enabled: !!currentMap?.mapId } },
	);
	const personalBest = pbQuery.data?.status === 200 ? pbQuery.data.data : null;

	if (isLoading) {
		return (
			<AppShell wide>
				<div className="flex flex-col gap-6">
					<Skeleton className="h-28 w-full rounded-xl" />
					<div className="grid grid-cols-1 gap-4 md:grid-cols-2">
						<Skeleton className="h-48 rounded-xl" />
						<Skeleton className="h-48 rounded-xl" />
					</div>
				</div>
			</AppShell>
		);
	}

	if (!onlineDevice) {
		return (
			<AppShell wide>
				<Empty className="mt-10">
					<EmptyHeader>
						<EmptyMedia variant="icon">
							<Monitor />
						</EmptyMedia>
						<EmptyTitle>No device connected</EmptyTitle>
						<EmptyDescription>
							Connect your VR headset to see live gameplay stats.
						</EmptyDescription>
					</EmptyHeader>
					<EmptyContent>
						<Button asChild>
							<Link to="/devices">Go to Devices</Link>
						</Button>
					</EmptyContent>
				</Empty>
			</AppShell>
		);
	}

	if (!currentMap) {
		return (
			<AppShell wide>
				<div className="flex flex-col gap-8 py-10">
					<div className="flex flex-col items-center gap-4">
						<div className="relative flex size-5">
							<span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-500 opacity-60" />
							<span className="relative inline-flex size-5 rounded-full bg-emerald-500" />
						</div>
						<div className="space-y-1 text-center">
							<p className="font-medium">{onlineDevice.name}</p>
							<p className="text-sm text-muted-foreground">
								Waiting for a map to start…
							</p>
						</div>
					</div>
					{/* When idle (i.e. done playing), recap the sitting just finished. */}
					<SessionSummary title="Your last session" />
				</div>
			</AppShell>
		);
	}

	const minutes = Math.floor(currentMap.durationMs / 60000);
	const seconds = Math.floor((currentMap.durationMs % 60000) / 1000);
	const duration = `${minutes}:${seconds.toString().padStart(2, "0")}`;
	const hasCover = currentMap.mapId !== null && !coverFailed;

	return (
		<AppShell wide>
			<div className="space-y-6">
				<MapHeader
					map={currentMap}
					duration={duration}
					hasCover={hasCover}
					onCoverError={() => setCoverFailed(true)}
				/>

				{recap && (
					<LiveSessionRecap results={recap} personalBest={personalBest} />
				)}

				<ScoreOverlay data={scoreUpdate} personalBest={personalBest} />
			</div>
		</AppShell>
	);
}

const DIFFICULTY_STYLES: Record<string, string> = {
	Easy: "border-emerald-500/25 bg-emerald-500/15 text-emerald-400",
	Normal: "border-sky-500/25 bg-sky-500/15 text-sky-400",
	Hard: "border-amber-500/25 bg-amber-500/15 text-amber-400",
	Expert: "border-rose-500/25 bg-rose-500/15 text-rose-400",
	ExpertPlus: "border-violet-500/25 bg-violet-500/15 text-violet-400",
};

function MapHeader({
	map,
	duration,
	hasCover,
	onCoverError,
}: {
	map: LiveMapStartedEvent;
	duration: string;
	hasCover: boolean;
	onCoverError: () => void;
}) {
	const maxSeconds = Math.floor(map.durationMs / 1000);
	const elapsed = useElapsedTime(map.timestamp, maxSeconds, map.songSpeed);
	const elapsedStr = formatClock(elapsed);
	const progress =
		maxSeconds > 0 ? Math.min((elapsed / maxSeconds) * 100, 100) : 0;

	return (
		<div className="relative flex items-stretch overflow-hidden rounded-xl border border-border bg-card">
			<div className="flex shrink-0 items-center justify-center bg-gradient-to-br from-primary/40 to-[oklch(0.62_0.19_255)]/40 p-3">
				{hasCover && map.mapId ? (
					<img
						src={getGetApiMapsMapIdCoverUrl(map.mapId)}
						alt={map.songName}
						onError={onCoverError}
						className="size-20 rounded-md object-cover shadow-sm"
					/>
				) : (
					<MusicPlaceholder />
				)}
			</div>

			<div className="flex min-w-0 flex-1 flex-col gap-2 p-4">
				<div className="flex items-center justify-between gap-2">
					<div className="flex items-center gap-2">
						<span className="relative flex size-2.5">
							<span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-red-500 opacity-75" />
							<span className="relative inline-flex size-2.5 rounded-full bg-red-500" />
						</span>
						<span className="text-xs font-semibold uppercase tracking-widest text-red-500">
							Live
						</span>
						<Badge
							variant="outline"
							className={cn(
								"border",
								DIFFICULTY_STYLES[map.difficulty] ??
									"border-border bg-muted text-muted-foreground",
							)}
						>
							{map.difficultyName}
						</Badge>
						<Badge variant="secondary">{map.characteristic}</Badge>
					</div>
					<span className="font-mono text-sm tabular-nums text-muted-foreground">
						<span className="text-foreground">{elapsedStr}</span>
						{" / "}
						{duration}
					</span>
				</div>

				<div className="min-w-0">
					<h1 className="truncate font-heading text-lg font-bold tracking-tight">
						{map.songName}
					</h1>
					<p className="truncate text-sm text-muted-foreground">
						by {map.songAuthor} · mapped by {map.mapper}
					</p>
				</div>

				<div className="mt-auto flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground">
					<InlineStat
						icon={<Pulse />}
						value={`${Math.round(map.bpm)}`}
						label="BPM"
					/>
					<InlineStat
						icon={<Fire />}
						value={map.notesPerSecond.toFixed(1)}
						label="NPS"
					/>
					<InlineStat
						icon={<MusicNote />}
						value={map.cuttableObjectCount.toString()}
						label="Notes"
					/>
					<InlineStat
						icon={<Dumbbell />}
						value={map.bombCount.toString()}
						label="Bombs"
					/>
					<InlineStat
						icon={<Widget />}
						value={map.obstacleCount.toString()}
						label="Walls"
					/>
				</div>
			</div>
			<div className="absolute inset-x-0 bottom-0 h-0.5 bg-border/50">
				<div
					className="h-full bg-primary transition-[width] duration-1000 ease-linear"
					style={{ width: `${progress}%` }}
				/>
			</div>
		</div>
	);
}

function formatClock(totalSeconds: number): string {
	const m = Math.floor(totalSeconds / 60);
	const s = totalSeconds % 60;
	return `${m}:${s.toString().padStart(2, "0")}`;
}

function useElapsedTime(
	startTimestamp: string,
	maxSeconds: number,
	songSpeed: number,
): number {
	const [elapsed, setElapsed] = useState(0);

	useEffect(() => {
		const start = new Date(startTimestamp).getTime();
		const update = () => {
			const realSeconds = (Date.now() - start) / 1000;
			const songSeconds = realSeconds * songSpeed;
			setElapsed(Math.min(Math.max(0, Math.floor(songSeconds)), maxSeconds));
		};
		update();
		const id = setInterval(update, 200);
		return () => clearInterval(id);
	}, [startTimestamp, maxSeconds, songSpeed]);

	return elapsed;
}

function InlineStat({
	icon,
	value,
	label,
}: {
	icon: React.ReactNode;
	value: string;
	label: string;
}) {
	return (
		<span className="flex items-center gap-1 font-mono tabular-nums [&_svg]:size-3.5">
			{icon}
			{value}
			<span className="text-muted-foreground/60">{label}</span>
		</span>
	);
}

function MusicPlaceholder() {
	return (
		<div className="flex size-20 items-center justify-center rounded-md bg-background/20">
			<MusicNotes className="size-8 text-muted-foreground/40" />
		</div>
	);
}

const RANK_STYLES: Record<string, string> = {
	SS: "text-amber-400",
	S: "text-fuchsia-400",
	A: "text-sky-400",
	B: "text-emerald-400",
	C: "text-yellow-400",
	D: "text-orange-400",
	E: "text-red-400",
};

function usePulseAnimation(
	value: string | number,
	animationName: string,
	duration: number,
) {
	const ref = useRef<HTMLSpanElement>(null);
	const prev = useRef(value);

	useEffect(() => {
		if (value === prev.current) return;
		prev.current = value;
		const el = ref.current;
		if (!el) return;
		el.style.animation = "none";
		void el.offsetHeight;
		el.style.animation = `${animationName} ${duration}ms ease-out`;
	}, [value, animationName, duration]);

	return ref;
}

const scoreFmt = new Intl.NumberFormat("en-US", { minimumIntegerDigits: 7 });

function ScoreOverlay({
	data,
	personalBest,
}: {
	data: ScoreUpdateEvent | null;
	personalBest: PersonalBestDto | null;
}) {
	const score = data?.score ?? 0;
	const rank = data?.rank ?? "—";
	const accuracy = data?.accuracy ?? 0;
	const energy = data?.energy ?? 1;
	const combo = data?.combo ?? 0;
	const misses = data?.misses ?? 0;
	const scoreRef = usePulseAnimation(score, "score-pulse", 150);
	const rankRef = usePulseAnimation(rank, "rank-pulse", 500);
	const multiplier = combo >= 8 ? 8 : combo >= 4 ? 4 : combo >= 2 ? 2 : 1;
	const multiplierRef = usePulseAnimation(multiplier, "rank-pulse", 500);

	const scoreStr = scoreFmt.format(score);
	const scoreReal = score.toLocaleString("en-US");
	const splitAt = scoreStr.length - scoreReal.length;

	const pbScore = personalBest ? Number(personalBest.score) : null;
	const beatenPb = pbScore !== null && score > pbScore;

	return (
		<div className="flex flex-col items-center gap-8 py-8">
			<div className="mx-auto grid w-fit grid-cols-2 items-center gap-x-8 gap-y-6 px-4 sm:grid-cols-3 md:grid-cols-5">
				<div className="col-span-full flex flex-col px-4 py-2">
					{pbScore !== null && (
						<div className="mb-1 flex items-center gap-2 text-xs font-medium">
							{beatenPb ? (
								<span className="rounded-full bg-amber-500/15 px-2 py-0.5 font-semibold uppercase tracking-wide text-amber-400">
									New personal best!
								</span>
							) : (
								<span className="text-muted-foreground">
									PB to beat:{" "}
									<span className="font-mono tabular-nums text-foreground">
										{pbScore.toLocaleString("en-US")}
									</span>
								</span>
							)}
						</div>
					)}
					<div className="flex items-center justify-between">
						<span
							ref={scoreRef}
							className={cn(
								"inline-block font-heading text-8xl font-bold tracking-normal tabular-nums md:text-9xl",
								beatenPb && "text-amber-400",
							)}
						>
							<span className="text-transparent">
								{scoreStr.slice(0, splitAt)}
							</span>
							{scoreStr.slice(splitAt)}
						</span>
						<MultiplierRing combo={combo} pulseRef={multiplierRef} />
					</div>
				</div>
				<div className="flex flex-col items-center gap-1">
					<span
						ref={rankRef}
						className={cn(
							"inline-block text-6xl font-bold",
							RANK_STYLES[rank] ?? "text-muted-foreground",
						)}
					>
						{rank}
					</span>
					<span className="text-xs uppercase tracking-wider text-muted-foreground/60">
						Rank
					</span>
				</div>
				<div className="flex flex-col items-center gap-1">
					<span className="flex items-center gap-2 font-mono text-xl tabular-nums">
						<Scale className="size-5 text-muted-foreground" />
						{(accuracy * 100).toFixed(1)}%
					</span>
					<span className="text-xs uppercase tracking-wider text-muted-foreground/60">
						Accuracy
					</span>
				</div>
				<div className="flex flex-col items-center gap-1">
					<div className="flex items-center gap-2">
						<HeartPulse className="size-5 text-muted-foreground" />
						<div className="h-2.5 w-24 overflow-hidden rounded-full bg-muted">
							<div
								className={cn(
									"h-full rounded-full transition-all duration-150",
									energy > 0.5
										? "bg-emerald-500"
										: energy > 0.25
											? "bg-amber-500"
											: "bg-red-500",
								)}
								style={{ width: `${energy * 100}%` }}
							/>
						</div>
					</div>
					<span className="text-xs uppercase tracking-wider text-muted-foreground/60">
						Health
					</span>
				</div>
				<div className="flex flex-col items-center gap-1">
					<span className="flex items-center gap-2 font-mono text-xl tabular-nums">
						<Bolt className="size-5 text-muted-foreground" />
						{combo}x
					</span>
					<span className="text-xs uppercase tracking-wider text-muted-foreground/60">
						Combo
					</span>
				</div>
				<div className="flex flex-col items-center gap-1">
					<span className="flex items-center gap-2 font-mono text-xl tabular-nums">
						<Target className="size-5 text-muted-foreground" />
						{misses}
					</span>
					<span className="text-xs uppercase tracking-wider text-muted-foreground/60">
						Misses
					</span>
				</div>
			</div>
		</div>
	);
}

function LiveSessionRecap({
	results,
	personalBest,
}: {
	results: MapResults;
	personalBest: PersonalBestDto | null;
}) {
	const pbScore = personalBest ? Number(personalBest.score) : null;
	const beatenPb = pbScore !== null && results.score > pbScore;

	return (
		<div className="mx-auto w-full max-w-2xl rounded-xl border border-primary/30 bg-gradient-to-br from-primary/10 to-transparent p-5">
			<div className="flex items-center justify-between gap-4">
				<div className="flex items-center gap-2">
					<span className="font-heading text-sm font-semibold text-muted-foreground">
						Play complete
					</span>
					{beatenPb && (
						<Badge className="border-amber-500/30 bg-amber-500/15 text-amber-400">
							New personal best!
						</Badge>
					)}
					{results.fullCombo && (
						<Badge variant="secondary" className="text-amber-400">
							Full combo
						</Badge>
					)}
				</div>
				<div className="flex items-baseline gap-3">
					<span
						className={cn(
							"font-heading text-2xl font-bold",
							RANK_STYLES[results.rank] ?? "text-muted-foreground",
						)}
					>
						{results.rank}
					</span>
					<span className="font-heading text-2xl font-bold tabular-nums">
						{results.score.toLocaleString("en-US")}
					</span>
					<span className="text-sm tabular-nums text-muted-foreground">
						{(results.accuracy * 100).toFixed(1)}%
					</span>
				</div>
			</div>
			<div className="mt-3 flex items-center justify-between gap-4 text-xs text-muted-foreground">
				<span>
					Max combo{" "}
					<span className="font-mono tabular-nums text-foreground">
						{results.maxCombo}x
					</span>
					<span className="mx-2">·</span>
					Misses{" "}
					<span className="font-mono tabular-nums text-foreground">
						{results.missedNotes}
					</span>
					{pbScore !== null && !beatenPb && (
						<>
							<span className="mx-2">·</span>
							PB{" "}
							<span className="font-mono tabular-nums text-foreground">
								{pbScore.toLocaleString("en-US")}
							</span>
						</>
					)}
				</span>
				<Link to="/plays" className="text-primary hover:underline">
					View plays
				</Link>
			</div>
		</div>
	);
}

const MULTIPLIER_TIERS = [
	{ threshold: 0, multiplier: 1, next: 2 },
	{ threshold: 2, multiplier: 2, next: 4 },
	{ threshold: 4, multiplier: 4, next: 8 },
	{ threshold: 8, multiplier: 8, next: null },
] as const;

function MultiplierRing({
	combo,
	pulseRef,
}: {
	combo: number;
	pulseRef: React.RefObject<HTMLSpanElement | null>;
}) {
	const tier = [...MULTIPLIER_TIERS]
		.reverse()
		.find((t) => combo >= t.threshold)!;
	const progress =
		tier.next !== null
			? (combo - tier.threshold) / (tier.next - tier.threshold)
			: 1;
	const r = 28;
	const c = 2 * Math.PI * r;

	return (
		<div className="relative flex size-28 shrink-0 items-center justify-center">
			<svg
				className="absolute size-full -rotate-90"
				viewBox="0 0 64 64"
				aria-hidden="true"
			>
				<circle
					cx="32"
					cy="32"
					r={r}
					fill="none"
					strokeWidth="3"
					className="stroke-muted/30"
				/>
				<circle
					cx="32"
					cy="32"
					r={r}
					fill="none"
					strokeWidth="3"
					strokeLinecap="round"
					strokeDasharray={c}
					strokeDashoffset={c * (1 - progress)}
					className="stroke-primary duration-300 ease-out [transition:stroke-dashoffset]"
				/>
			</svg>
			<span
				ref={pulseRef}
				className="inline-block font-heading text-5xl font-bold tabular-nums text-primary"
			>
				{tier.multiplier}x
			</span>
		</div>
	);
}

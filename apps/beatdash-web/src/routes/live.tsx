import { useEffect, useState } from "react";
import { createFileRoute, redirect, Link } from "@tanstack/react-router";
import { useQueryClient } from "@tanstack/react-query";
import {
	MonitorIcon,
	MetronomeIcon,
	MusicNotesSimpleIcon,
	FireIcon,
	GuitarIcon,
	BarbellIcon,
	WallIcon,
} from "@phosphor-icons/react";
import { AppShell } from "@/components/layout/AppShell";
import { useRealtimeEvent, type LiveMapStartedEvent } from "@/realtime";
import { getGetApiDeviceQueryKey, useGetApiDevice } from "@/api/device/device";
import { getGetApiMapsMapIdCoverUrl } from "@/api/maps/maps";
import { Badge } from "@shiron/ui/components/ui/badge";
import { Button } from "@shiron/ui/components/ui/button";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import { cn } from "@shiron/ui/lib/utils";
import {
	Empty,
	EmptyContent,
	EmptyDescription,
	EmptyHeader,
	EmptyMedia,
	EmptyTitle,
} from "@shiron/ui/components/ui/empty";

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
	const [coverFailed, setCoverFailed] = useState(false);
	const { data, isLoading } = useGetApiDevice();
	const devices = data?.status === 200 ? data.data : [];
	const onlineDevice = devices.find((d) => d.session != null);

	const queryClient = useQueryClient();
	useRealtimeEvent("receiveDeviceStatus", (event) => {
		if (!event.isOnline) {
			setCurrentMap(null);
		}
		queryClient.invalidateQueries({
			queryKey: getGetApiDeviceQueryKey(),
		});
	});
	useRealtimeEvent("receiveLiveMapStarted", (event) => {
		setCurrentMap(event);
		setCoverFailed(false);
	});

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
							<MonitorIcon />
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
				<div className="flex flex-col items-center gap-6 py-24">
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

				<div className="grid grid-cols-1 gap-4 md:grid-cols-2">
					<PlaceholderCard title="Performance Charts" />
					<PlaceholderCard title="Current Rank" />
				</div>
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
	const elapsed = useElapsedTime(map.timestamp, maxSeconds);
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
						icon={<MetronomeIcon />}
						value={`${Math.round(map.bpm)}`}
						label="BPM"
					/>
					<InlineStat
						icon={<FireIcon />}
						value={map.notesPerSecond.toFixed(1)}
						label="NPS"
					/>
					<InlineStat
						icon={<GuitarIcon />}
						value={map.cuttableObjectCount.toString()}
						label="Notes"
					/>
					<InlineStat
						icon={<BarbellIcon />}
						value={map.bombCount.toString()}
						label="Bombs"
					/>
					<InlineStat
						icon={<WallIcon />}
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

function useElapsedTime(startTimestamp: string, maxSeconds: number): number {
	const [elapsed, setElapsed] = useState(0);

	useEffect(() => {
		const start = new Date(startTimestamp).getTime();
		const update = () => {
			const seconds = Math.floor((Date.now() - start) / 1000);
			setElapsed(Math.min(Math.max(0, seconds), maxSeconds));
		};
		update();
		const id = setInterval(update, 1000);
		return () => clearInterval(id);
	}, [startTimestamp, maxSeconds]);

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
			<MusicNotesSimpleIcon className="size-8 text-muted-foreground/40" />
		</div>
	);
}

function PlaceholderCard({ title }: { title: string }) {
	return (
		<div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-border bg-card/30 p-12">
			<p className="text-sm font-medium text-muted-foreground">{title}</p>
			<p className="mt-1 text-xs text-muted-foreground/60">Coming soon</p>
		</div>
	);
}

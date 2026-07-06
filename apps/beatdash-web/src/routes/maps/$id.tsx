import {
	ArrowLeftIcon,
	FireIcon,
	MetronomeIcon,
	MusicNotesSimpleIcon,
	SealCheckIcon,
	ThumbsUpIcon,
	TimerIcon,
} from "@phosphor-icons/react";
import { Badge } from "@shiron/ui/components/ui/badge";
import { Button } from "@shiron/ui/components/ui/button";
import {
	Card,
	CardContent,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import {
	Empty,
	EmptyDescription,
	EmptyHeader,
	EmptyMedia,
	EmptyTitle,
} from "@shiron/ui/components/ui/empty";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import { cn } from "@shiron/ui/lib/utils";
import { createFileRoute, Link, redirect } from "@tanstack/react-router";
import { formatDistanceToNow } from "date-fns";
import { useState } from "react";
import {
	getGetApiMapsMapIdCoverUrl,
	useGetApiMapsMapId,
} from "@/api/maps/maps";
import type { BeatmapDifficultyDto } from "@/api/model";
import { AppShell } from "@/components/layout/AppShell";

const RANK_ORDER = ["Easy", "Normal", "Hard", "Expert", "ExpertPlus"] as const;

const RANK_STYLES: Record<string, string> = {
	Easy: "border-emerald-500/25 bg-emerald-500/15 text-emerald-400",
	Normal: "border-sky-500/25 bg-sky-500/15 text-sky-400",
	Hard: "border-amber-500/25 bg-amber-500/15 text-amber-400",
	Expert: "border-rose-500/25 bg-rose-500/15 text-rose-400",
	ExpertPlus: "border-violet-500/25 bg-violet-500/15 text-violet-400",
};

const RANK_LABELS: Record<string, string> = { ExpertPlus: "Expert+" };

const CHARACTERISTICS = [
	{ key: "stream", label: "Stream" },
	{ key: "tech", label: "Tech" },
	{ key: "speed", label: "Speed" },
	{ key: "jumps", label: "Jumps" },
	{ key: "gimmick", label: "Gimmick" },
] as const;

function n(value: number | string | null | undefined): number {
	return value == null ? 0 : Number(value);
}

function formatDuration(ms: number): string {
	const total = Math.floor(ms / 1000);
	return `${Math.floor(total / 60)}:${(total % 60).toString().padStart(2, "0")}`;
}

function rankIndex(rank: string): number {
	const idx = RANK_ORDER.indexOf(rank as (typeof RANK_ORDER)[number]);
	return idx === -1 ? Number.MAX_SAFE_INTEGER : idx;
}

export const Route = createFileRoute("/maps/$id")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	component: MapDetailPage,
});

function MapDetailPage() {
	const { id } = Route.useParams();
	const { data, isLoading } = useGetApiMapsMapId(id);
	const map = data?.status === 200 ? data.data : undefined;
	const [coverFailed, setCoverFailed] = useState(false);

	const difficulties = map
		? [...map.difficulties].sort(
				(a, b) =>
					a.characteristicSerializedName.localeCompare(
						b.characteristicSerializedName,
					) || rankIndex(a.difficultyRank) - rankIndex(b.difficultyRank),
			)
		: [];

	return (
		<AppShell>
			<Button variant="ghost" size="sm" asChild className="-ml-2 mb-4">
				<Link to="/maps">
					<ArrowLeftIcon className="size-4" />
					Maps
				</Link>
			</Button>

			{isLoading && (
				<div className="space-y-4">
					<Skeleton className="h-28 rounded-xl" />
					<Skeleton className="h-40 rounded-xl" />
					<Skeleton className="h-40 rounded-xl" />
				</div>
			)}

			{!isLoading && !map && (
				<Empty className="mt-10">
					<EmptyHeader>
						<EmptyMedia variant="icon">
							<MusicNotesSimpleIcon />
						</EmptyMedia>
						<EmptyTitle>Map not found</EmptyTitle>
						<EmptyDescription>
							This map may not exist or hasn't been imported yet.
						</EmptyDescription>
					</EmptyHeader>
				</Empty>
			)}

			{!isLoading && map && (
				<div className="space-y-4">
					{/* Header */}
					<div className="flex gap-4 rounded-xl border border-border bg-card p-4">
						<div className="flex size-28 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-gradient-to-br from-primary/40 to-[oklch(0.62_0.19_255)]/40">
							{map.coverImageKey && !coverFailed ? (
								<img
									src={getGetApiMapsMapIdCoverUrl(map.id)}
									alt={map.songName}
									onError={() => setCoverFailed(true)}
									className="size-full object-cover"
								/>
							) : (
								<MusicNotesSimpleIcon className="size-10 text-muted-foreground/40" />
							)}
						</div>

						<div className="flex min-w-0 flex-1 flex-col">
							<h1 className="font-heading text-xl font-semibold tracking-tight">
								{map.songName}
								{map.songSubName && (
									<span className="ml-2 text-base font-normal text-muted-foreground">
										{map.songSubName}
									</span>
								)}
							</h1>
							<p className="text-sm text-muted-foreground">
								{map.songAuthor}
								<span className="mx-1.5">·</span>
								mapped by {map.mapper}
							</p>

							<div className="mt-auto flex flex-wrap items-center gap-3 pt-2 text-sm text-muted-foreground">
								<Stat icon={<MetronomeIcon />} value={`${n(map.bpm)} BPM`} />
								<Stat
									icon={<TimerIcon />}
									value={formatDuration(n(map.durationMs))}
								/>
								<Badge
									variant="outline"
									className="border-border text-muted-foreground"
								>
									{map.fetchStatus}
								</Badge>
								{map.beatSaver?.ranked && (
									<Badge className="gap-1 border-emerald-500/25 bg-emerald-500/15 text-emerald-400">
										<SealCheckIcon className="size-3.5" weight="fill" />
										Ranked
									</Badge>
								)}
							</div>
						</div>
					</div>

					{/* BeatSaver */}
					{map.beatSaver && (
						<Card>
							<CardHeader>
								<CardTitle className="text-sm">BeatSaver</CardTitle>
							</CardHeader>
							<CardContent className="space-y-3 text-sm">
								<div className="flex flex-wrap items-center gap-4 text-muted-foreground">
									<span className="flex items-center gap-1.5 text-foreground">
										<ThumbsUpIcon className="size-4" weight="fill" />
										<span className="font-mono tabular-nums">
											{n(map.beatSaver.upvotes).toLocaleString()}
										</span>
										<span className="text-muted-foreground">
											/ {n(map.beatSaver.downvotes).toLocaleString()} down
										</span>
									</span>
									<span>
										Rating{" "}
										<span className="font-mono tabular-nums text-foreground">
											{(n(map.beatSaver.score) * 100).toFixed(1)}%
										</span>
									</span>
									<span>
										by{" "}
										<span className="text-foreground">
											{map.beatSaver.uploader ?? "unknown"}
										</span>
									</span>
									{map.beatSaver.uploaded && (
										<span>
											{formatDistanceToNow(new Date(map.beatSaver.uploaded), {
												addSuffix: true,
											})}
										</span>
									)}
								</div>

								{map.beatSaver.description && (
									<p className="whitespace-pre-line text-muted-foreground">
										{map.beatSaver.description}
									</p>
								)}

								{map.beatSaver.tags.length > 0 && (
									<div className="flex flex-wrap gap-1.5">
										{map.beatSaver.tags.map((tag) => (
											<Badge
												key={tag}
												variant="outline"
												className="border-border text-muted-foreground"
											>
												{tag}
											</Badge>
										))}
									</div>
								)}
							</CardContent>
						</Card>
					)}

					{/* Difficulties */}
					<div className="space-y-3">
						{difficulties.map((d) => (
							<DifficultyCard key={d.id} difficulty={d} />
						))}
					</div>
				</div>
			)}
		</AppShell>
	);
}

function DifficultyCard({ difficulty }: { difficulty: BeatmapDifficultyDto }) {
	const a = difficulty.analysis;
	const analyzed = a?.metricStatus === "Success";
	const chars = a?.characteristics ?? null;

	return (
		<Card>
			<CardHeader className="flex-row items-center justify-between gap-2 space-y-0">
				<div className="flex items-center gap-2">
					<Badge
						variant="outline"
						className={cn(
							"border",
							RANK_STYLES[difficulty.difficultyRank] ??
								"border-border bg-muted text-muted-foreground",
						)}
					>
						{RANK_LABELS[difficulty.difficultyRank] ??
							difficulty.difficultyRank}
					</Badge>
					<CardTitle className="text-sm">{difficulty.difficultyName}</CardTitle>
					{difficulty.characteristicSerializedName !== "Standard" && (
						<span className="text-xs text-muted-foreground">
							{difficulty.characteristicSerializedName}
						</span>
					)}
				</div>
				{difficulty.noteJumpSpeed != null && (
					<span className="font-mono text-xs tabular-nums text-muted-foreground">
						NJS {n(difficulty.noteJumpSpeed)}
					</span>
				)}
			</CardHeader>

			<CardContent className="space-y-4">
				<div className="flex flex-wrap gap-x-5 gap-y-1 text-xs text-muted-foreground">
					<Stat
						icon={<FireIcon />}
						value={`${n(difficulty.notesPerSecond).toFixed(1)} NPS`}
					/>
					<span>{n(difficulty.cuttableObjectCount)} notes</span>
					<span>{n(difficulty.bombCount)} bombs</span>
					<span>{n(difficulty.obstacleCount)} walls</span>
				</div>

				{!analyzed && (
					<p className="text-xs text-muted-foreground/70">
						{a
							? `Metrics not available (${a.metricStatus}).`
							: "Not analyzed yet — the map is still being fetched or processed."}
					</p>
				)}

				{analyzed && (
					<div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
						<div className="grid grid-cols-2 gap-3">
							<Headline label="Difficulty" value={n(a.difficultyRating)} />
							<Headline label="PP" value={n(a.pp)} decimals={0} />
						</div>
						<div className="space-y-2">
							{CHARACTERISTICS.map((c) => (
								<MetricBar
									key={c.key}
									label={c.label}
									value={n(chars?.[c.key])}
								/>
							))}
						</div>
					</div>
				)}
			</CardContent>
		</Card>
	);
}

function Headline({
	label,
	value,
	decimals = 2,
}: {
	label: string;
	value: number;
	decimals?: number;
}) {
	return (
		<div className="rounded-lg border border-border bg-muted/30 p-3">
			<p className="text-xs text-muted-foreground">{label}</p>
			<p className="font-mono text-2xl font-semibold tabular-nums">
				{value.toFixed(decimals)}
			</p>
		</div>
	);
}

function MetricBar({ label, value }: { label: string; value: number }) {
	const pct = Math.round(Math.max(0, Math.min(1, value)) * 100);
	return (
		<div className="space-y-1">
			<div className="flex items-center justify-between text-xs">
				<span className="text-muted-foreground">{label}</span>
				<span className="font-mono tabular-nums">{value.toFixed(2)}</span>
			</div>
			<div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
				<div
					className="h-full rounded-full bg-primary transition-[width]"
					style={{ width: `${pct}%` }}
				/>
			</div>
		</div>
	);
}

function Stat({ icon, value }: { icon: React.ReactNode; value: string }) {
	return (
		<span className="flex items-center gap-1.5 font-mono tabular-nums [&_svg]:size-4">
			{icon}
			{value}
		</span>
	);
}

import { Badge } from "@shiron/ui/components/ui/badge";
import {
	Tooltip,
	TooltipContent,
	TooltipTrigger,
} from "@shiron/ui/components/ui/tooltip";
import { cn } from "@shiron/ui/lib/utils";
import { MusicNotes } from "@solar-icons/react";
import { Link } from "@tanstack/react-router";
import { format } from "date-fns";
import { useMemo, useState } from "react";
import { getGetApiMapsMapIdCoverUrl } from "@/api/maps/maps";
import type { PlaySessionListItemDto } from "@/api/model";
import {
	DIFFICULTY_TEXT_STYLES,
	formatAccuracy,
	formatDuration,
	parseDurationMs,
	RANK_BG_STYLES,
	RANK_STYLES,
} from "@/lib/sessions";

/** A play with its clock position resolved onto the session's time axis. */
interface TimelinePlay {
	play: PlaySessionListItemDto;
	startMs: number;
	durationMs: number;
	/** 0–1 offset of the play's start within the session span. */
	left: number;
	/** 0–1 fraction of the session span the play occupies. */
	width: number;
}

/** Segments narrower than this (as a fraction) are padded so they stay tappable. */
const MIN_SEGMENT = 0.012;

function coverUrl(beatmapId: string): string {
	return getGetApiMapsMapIdCoverUrl(beatmapId);
}

/**
 * A horizontal, time-scaled view of a single sitting: every played song is a
 * compact card, and the color-coded bar beneath places each play on the clock
 * (x-axis) and grades it. Cards and bar segments cross-highlight on hover, and
 * both link straight to the matching play.
 */
export function SessionTimeline({
	plays,
}: {
	plays: PlaySessionListItemDto[];
}) {
	const [hoveredId, setHoveredId] = useState<string | null>(null);

	const { items, startLabel, endLabel, spanLabel } = useMemo(() => {
		const sorted = [...plays].sort(
			(a, b) => Date.parse(a.startedAt) - Date.parse(b.startedAt),
		);

		const withTime = sorted.map((play) => {
			const startMs = Date.parse(play.startedAt);
			// In-progress plays report no duration; give them a nominal slice so the
			// bar segment stays visible rather than collapsing to nothing.
			const durationMs = parseDurationMs(play.duration) || 60_000;
			return { play, startMs, durationMs };
		});

		const first = withTime[0]?.startMs ?? 0;
		const last = withTime.reduce(
			(max, p) => Math.max(max, p.startMs + p.durationMs),
			first,
		);
		const span = Math.max(last - first, 1);

		const resolved: TimelinePlay[] = withTime.map((p) => ({
			...p,
			left: (p.startMs - first) / span,
			width: Math.max(p.durationMs / span, MIN_SEGMENT),
		}));

		return {
			items: resolved,
			startLabel: withTime.length ? format(first, "HH:mm") : "",
			endLabel: withTime.length ? format(last, "HH:mm") : "",
			spanLabel: formatDuration(msToTimeSpan(span)),
		};
	}, [plays]);

	if (items.length === 0) return null;

	return (
		<div className="flex flex-col gap-3">
			<div className="flex items-center justify-between">
				<span className="text-xs text-muted-foreground">Session timeline</span>
				<span className="font-mono text-[10px] tabular-nums text-muted-foreground/70">
					{startLabel} – {endLabel} · {spanLabel}
				</span>
			</div>

			{/* Cards: the played songs, chronological, each a shortcut into the play. */}
			<div className="-mx-1 flex gap-2 overflow-x-auto px-1 pb-1">
				{items.map(({ play }) => (
					<TimelineCard
						key={play.id}
						play={play}
						active={hoveredId === play.id}
						onHover={setHoveredId}
					/>
				))}
			</div>

			{/* Bar: the time axis. Segment width ∝ play length, color = grade. */}
			<div className="relative h-2.5 w-full overflow-hidden rounded-full bg-muted/40">
				{items.map(({ play, left, width }) => {
					const rank = play.results?.rank;
					return (
						<Tooltip key={play.id}>
							<TooltipTrigger asChild>
								<Link
									to="/plays/$id"
									params={{ id: play.id }}
									onMouseEnter={() => setHoveredId(play.id)}
									onMouseLeave={() => setHoveredId(null)}
									aria-label={`${play.songName} — ${rank ?? "in progress"}`}
									className={cn(
										"absolute inset-y-0 rounded-full transition-all duration-150",
										rank ? RANK_BG_STYLES[rank] : "bg-muted-foreground/40",
										hoveredId === play.id
											? "opacity-100 ring-1 ring-foreground/50"
											: "opacity-80 hover:opacity-100",
									)}
									style={{
										left: `${left * 100}%`,
										width: `${width * 100}%`,
									}}
								/>
							</TooltipTrigger>
							<TooltipContent className="flex flex-col gap-0.5">
								<span className="font-semibold">{play.songName}</span>
								<span className="text-muted-foreground">
									{play.difficultyName}
									{play.results
										? ` · ${play.results.rank} · ${formatAccuracy(play.results.accuracy)}`
										: " · In progress"}
								</span>
							</TooltipContent>
						</Tooltip>
					);
				})}
			</div>
		</div>
	);
}

function TimelineCard({
	play,
	active,
	onHover,
}: {
	play: PlaySessionListItemDto;
	active: boolean;
	onHover: (id: string | null) => void;
}) {
	const [coverFailed, setCoverFailed] = useState(false);
	const results = play.results;
	const rank = results?.rank;

	return (
		<Link
			to="/plays/$id"
			params={{ id: play.id }}
			onMouseEnter={() => onHover(play.id)}
			onMouseLeave={() => onHover(null)}
			className={cn(
				"group relative flex w-40 shrink-0 flex-col gap-1.5 overflow-hidden rounded-lg border bg-card p-2 transition-colors",
				active
					? "border-primary/50 bg-accent/40"
					: "border-border hover:border-primary/40 hover:bg-accent/30",
			)}
		>
			{/* Grade accent stripe down the left edge. */}
			<span
				className={cn(
					"absolute inset-y-0 left-0 w-1",
					rank ? RANK_BG_STYLES[rank] : "bg-muted-foreground/30",
				)}
			/>

			<div className="flex items-center gap-2 pl-1">
				<div className="size-9 shrink-0 overflow-hidden rounded-md bg-gradient-to-br from-primary/40 to-[oklch(0.62_0.19_255)]/40">
					{!coverFailed ? (
						<img
							src={coverUrl(play.beatmapId)}
							alt={play.songName}
							loading="lazy"
							onError={() => setCoverFailed(true)}
							className="size-full object-cover"
						/>
					) : (
						<div className="flex size-full items-center justify-center">
							<MusicNotes className="size-4 text-muted-foreground/40" />
						</div>
					)}
				</div>
				<div className="min-w-0 flex-1">
					<span className="block truncate font-heading text-xs font-semibold">
						{play.songName}
					</span>
					<span className="font-mono text-[10px] tabular-nums text-muted-foreground">
						{format(new Date(play.startedAt), "HH:mm")}
					</span>
				</div>
			</div>

			<div className="flex items-center justify-between pl-1">
				<Badge
					variant="outline"
					className={cn(
						"h-4 border-transparent bg-muted/50 px-1 text-[9px]",
						DIFFICULTY_TEXT_STYLES[play.difficultyRank] ??
							"text-muted-foreground",
					)}
				>
					{play.difficultyName}
				</Badge>
				{results ? (
					<span className="flex items-baseline gap-1">
						<span
							className={cn(
								"font-heading text-sm font-bold leading-none",
								RANK_STYLES[results.rank] ?? "text-muted-foreground",
							)}
						>
							{results.rank}
						</span>
						<span className="font-mono text-[10px] tabular-nums text-muted-foreground">
							{formatAccuracy(results.accuracy)}
						</span>
					</span>
				) : (
					<span className="text-[10px] text-muted-foreground">Live</span>
				)}
			</div>
		</Link>
	);
}

/** Formats a raw millisecond span back into a `hh:mm:ss` string for reuse of {@link formatDuration}. */
function msToTimeSpan(ms: number): string {
	const totalSeconds = Math.floor(ms / 1000);
	const h = Math.floor(totalSeconds / 3600);
	const m = Math.floor((totalSeconds % 3600) / 60);
	const s = totalSeconds % 60;
	return `${h.toString().padStart(2, "0")}:${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
}

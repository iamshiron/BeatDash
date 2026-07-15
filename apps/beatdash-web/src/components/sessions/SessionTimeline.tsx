import { Badge } from "@shiron/ui/components/ui/badge";
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

/** Fixed card width (px); the timeline scale is derived from this. */
const CARD_W = 176;
/** Minimum clear space between two adjacent card starts. */
const GUTTER = 16;
/**
 * The tightest gap between two play starts we scale for. A shorter real gap
 * (a quick retry) is allowed to overlap slightly rather than blow the whole
 * timeline out of proportion — the scale stays uniform everywhere.
 */
const MIN_SPACING_MS = 30_000;
/** Layout bands within the timeline, top-down. */
const CARD_H = 76;
const CONNECTOR_H = 16;
const BAR_H = 10;
const BAR_Y = CARD_H + CONNECTOR_H;
const TOTAL_H = BAR_Y + BAR_H;
const MIN_SEGMENT_PX = 6;
/** Width (px) of the grade accent — matches the card's `w-1` left stripe. */
const ACCENT_W = 4;
/**
 * The funnel is drawn this many px past its band at both ends, tucking under
 * the card above and over the bar below. The overlap is hidden by those layers
 * but removes the sub-pixel hairline seams a flush edge-to-edge join leaves.
 */
const OVERLAP = 1.5;

interface TimelinePlay {
	play: PlaySessionListItemDto;
	/** Left offset (px, pixel-snapped) of the play's start on the time axis. */
	x: number;
	/** Segment width (px, pixel-snapped) — the play's length at the scale. */
	segWidth: number;
}

function coverUrl(beatmapId: string): string {
	return getGetApiMapsMapIdCoverUrl(beatmapId);
}

/**
 * A single sitting laid out on one uniformly-scaled time axis. Every played
 * song is a compact card anchored at its real start time and funnels down to
 * its color-coded segment on the shared bar below. Cards and bar scroll
 * together and both link straight to the play.
 */
export function SessionTimeline({
	plays,
}: {
	plays: PlaySessionListItemDto[];
}) {
	const [hoveredId, setHoveredId] = useState<string | null>(null);

	const { items, totalWidth, startLabel, endLabel, spanLabel } = useMemo(() => {
		const sorted = [...plays]
			.map((play) => ({
				play,
				startMs: Date.parse(play.startedAt),
				// In-progress plays report no duration; give them a nominal length.
				durationMs: parseDurationMs(play.duration) || 60_000,
			}))
			.sort((a, b) => a.startMs - b.startMs);

		const first = sorted[0]?.startMs ?? 0;
		const last = sorted.reduce(
			(max, p) => Math.max(max, p.startMs + p.durationMs),
			first,
		);
		const span = Math.max(last - first, 1);

		// One scale (px per ms) applied to the whole axis, so distances are a true
		// reflection of elapsed time. It's fixed by the closest pair of starts so
		// their cards clear each other.
		const gaps = sorted
			.slice(1)
			.map((p, i) => p.startMs - sorted[i].startMs)
			.filter((d) => d > 0);
		const tightest = gaps.length ? Math.min(...gaps) : span;
		const pxPerMs = (CARD_W + GUTTER) / Math.max(tightest, MIN_SPACING_MS);

		// Snap edges to whole pixels so the card / funnel / segment share exact
		// pixel boundaries instead of landing on fractional, antialiased ones.
		const resolved: TimelinePlay[] = sorted.map((p) => ({
			play: p.play,
			x: Math.round((p.startMs - first) * pxPerMs),
			segWidth: Math.max(Math.round(p.durationMs * pxPerMs), MIN_SEGMENT_PX),
		}));

		const width = resolved.reduce(
			(max, it) => Math.max(max, it.x + CARD_W, it.x + it.segWidth),
			CARD_W,
		);

		return {
			items: resolved,
			totalWidth: width,
			startLabel: sorted.length ? format(first, "HH:mm") : "",
			endLabel: sorted.length ? format(last, "HH:mm") : "",
			spanLabel: formatDuration(msToTimeSpan(span)),
		};
	}, [plays]);

	if (items.length === 0) return null;

	// Funnel band, grown by OVERLAP at both ends (see OVERLAP).
	const funnelTop = CARD_H - OVERLAP;
	const funnelH = CONNECTOR_H + OVERLAP * 2;

	return (
		<div className="flex flex-col gap-3">
			<div className="flex items-center justify-between">
				<span className="text-xs text-muted-foreground">Session timeline</span>
				<span className="font-mono text-[10px] tabular-nums text-muted-foreground/70">
					{startLabel} – {endLabel} · {spanLabel}
				</span>
			</div>

			<div className="-mx-1 overflow-x-auto px-1 pb-1">
				<div
					className="relative"
					style={{ width: totalWidth, height: TOTAL_H }}
				>
					{/* Idle time shows through as the empty stretches of this track. */}
					<div
						className="absolute rounded-full bg-muted/40"
						style={{ left: 0, width: totalWidth, top: BAR_Y, height: BAR_H }}
					/>

					{items.map(({ play, x, segWidth }) => {
						const rank = play.results?.rank;
						const active = hoveredId === play.id;
						const barColor = rank
							? RANK_BG_STYLES[rank]
							: "bg-muted-foreground/40";
						return (
							<Link
								key={`seg-${play.id}`}
								to="/plays/$id"
								params={{ id: play.id }}
								title={`${play.songName} · ${rank ?? "In progress"}`}
								onMouseEnter={() => setHoveredId(play.id)}
								onMouseLeave={() => setHoveredId(null)}
								className={cn(
									"absolute rounded-full transition-all duration-150",
									barColor,
									active
										? "opacity-100 ring-1 ring-foreground/50"
										: "opacity-85 hover:opacity-100",
								)}
								style={{ left: x, width: segWidth, top: BAR_Y, height: BAR_H }}
							/>
						);
					})}

					{/* Funnel each card down to its segment as a silhouette continuation:
					    the left edge carries the card's grade accent stripe (same 4px
					    width) straight down, the right edge carries the card's 1px
					    border in toward the segment's end. Coordinates run 0..funnelH,
					    so y=0 tucks under the card and y=funnelH tucks over the bar. */}
					<svg
						className="pointer-events-none absolute"
						style={{
							left: 0,
							top: funnelTop,
							width: totalWidth,
							height: funnelH,
						}}
						width={totalWidth}
						height={funnelH}
						aria-hidden
					>
						<title>
							Connectors from each play card to its timeline segment
						</title>
						{items.map(({ play, x, segWidth }) => {
							const rank = play.results?.rank;
							const color = rank
								? RANK_STYLES[rank]
								: "text-muted-foreground/50";
							const active = hoveredId === play.id;
							return (
								<g key={`link-${play.id}`} className={color}>
									<polygon
										points={`${x},0 ${x + CARD_W},0 ${x + segWidth},${funnelH} ${x},${funnelH}`}
										className="fill-current"
										fillOpacity={active ? 0.24 : 0.14}
									/>
									{/* Left edge = the accent stripe carried down (x → x+4). The
									    g's grade text color feeds `fill-current`, matching the
									    card stripe's identical grade shade. */}
									<rect
										x={x}
										y={0}
										width={ACCENT_W}
										height={funnelH}
										className="fill-current"
									/>
									{/* Right edge = the card's border carried in to the segment. */}
									<line
										x1={x + CARD_W}
										y1={0}
										x2={x + segWidth}
										y2={funnelH}
										strokeWidth={1}
										className={active ? "stroke-primary/50" : "stroke-border"}
									/>
								</g>
							);
						})}
					</svg>

					{items.map(({ play, x }) => (
						<TimelineCard
							key={`card-${play.id}`}
							play={play}
							x={x}
							active={hoveredId === play.id}
							onHover={setHoveredId}
						/>
					))}
				</div>
			</div>
		</div>
	);
}

function TimelineCard({
	play,
	x,
	active,
	onHover,
}: {
	play: PlaySessionListItemDto;
	x: number;
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
			style={{ left: x, width: CARD_W, top: 0, height: CARD_H }}
			className={cn(
				// No left/bottom border: the grade accent stripe is the left edge, and
				// the card flows straight into the funnel below.
				"absolute flex flex-col justify-between overflow-hidden rounded-t-lg border border-b-0 border-l-0 bg-card p-2 transition-colors",
				active
					? "z-10 border-primary/50 bg-accent/40"
					: "border-border hover:border-primary/40 hover:bg-accent/30",
			)}
		>
			{/* Grade accent down the left edge. */}
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

/** Formats a raw millisecond span into `hh:mm:ss` for reuse of {@link formatDuration}. */
function msToTimeSpan(ms: number): string {
	const totalSeconds = Math.floor(ms / 1000);
	const h = Math.floor(totalSeconds / 3600);
	const m = Math.floor((totalSeconds % 3600) / 60);
	const s = totalSeconds % 60;
	return `${h.toString().padStart(2, "0")}:${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
}

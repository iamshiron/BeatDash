import {
	FireIcon,
	MetronomeIcon,
	MusicNotesSimpleIcon,
	TimerIcon,
} from "@phosphor-icons/react";
import { Badge, badgeVariants } from "@shiron/ui/components/ui/badge";
import {
	Tooltip,
	TooltipContent,
	TooltipTrigger,
} from "@shiron/ui/components/ui/tooltip";
import { cn } from "@shiron/ui/lib/utils";
import { useCallback, useLayoutEffect, useRef, useState } from "react";
import { getGetApiMapsMapIdCoverUrl } from "@/api/maps/maps";
import type { BeatmapDifficultyDto, MapDetailDto } from "@/api/model";

const RANK_ORDER = ["Easy", "Normal", "Hard", "Expert", "ExpertPlus"] as const;

const RANK_STYLES: Record<string, string> = {
	Easy: "border-emerald-500/25 bg-emerald-500/15 text-emerald-400",
	Normal: "border-sky-500/25 bg-sky-500/15 text-sky-400",
	Hard: "border-amber-500/25 bg-amber-500/15 text-amber-400",
	Expert: "border-rose-500/25 bg-rose-500/15 text-rose-400",
	ExpertPlus: "border-violet-500/25 bg-violet-500/15 text-violet-400",
};

function num(value: number | string): number {
	return Number(value);
}

function formatDuration(ms: number): string {
	const totalSeconds = Math.floor(ms / 1000);
	const minutes = Math.floor(totalSeconds / 60);
	const seconds = totalSeconds % 60;
	return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

function rankIndex(rank: string): number {
	const idx = RANK_ORDER.indexOf(rank as (typeof RANK_ORDER)[number]);
	return idx === -1 ? Number.MAX_SAFE_INTEGER : idx;
}

export function MapCard({ map }: { map: MapDetailDto }) {
	const [coverFailed, setCoverFailed] = useState(false);
	const hasCover = map.coverImageKey != null && !coverFailed;

	const difficulties = [...map.difficulties].sort(
		(a, b) => rankIndex(a.difficultyRank) - rankIndex(b.difficultyRank),
	);

	const maxNps = difficulties.reduce(
		(max, d) => Math.max(max, num(d.notesPerSecond)),
		0,
	);

	return (
		<div className="flex overflow-hidden rounded-xl border border-border bg-card transition-colors hover:border-primary/40 focus-within:border-primary/40">
			<div className="flex shrink-0 items-center justify-center bg-gradient-to-br from-primary/40 to-[oklch(0.62_0.19_255)]/40 p-3">
				{hasCover ? (
					<img
						src={getGetApiMapsMapIdCoverUrl(map.id)}
						alt={map.songName}
						loading="lazy"
						onError={() => setCoverFailed(true)}
						className="size-20 rounded-md object-cover shadow-sm"
					/>
				) : (
					<MusicNotesSimpleIcon className="size-9 text-muted-foreground/40" />
				)}
			</div>

			<div className="flex min-w-0 flex-1 flex-col gap-2 p-3">
				<div className="min-w-0">
					<TruncatedText className="font-heading text-sm font-semibold">
						{map.songName}
					</TruncatedText>
					{(map.songSubName || map.songAuthor) && (
						<TruncatedText className="text-xs text-muted-foreground">
							{[map.songAuthor, map.songSubName].filter(Boolean).join(" — ")}
						</TruncatedText>
					)}
				</div>

				<DifficultyTags difficulties={difficulties} />

				<div className="mt-auto flex items-center gap-3 text-xs text-muted-foreground">
					<Stat
						icon={<MetronomeIcon />}
						value={`${num(map.bpm)}`}
						label="BPM"
					/>
					<Stat
						icon={<TimerIcon />}
						value={formatDuration(num(map.durationMs))}
						label="Length"
					/>
					<Stat
						icon={<FireIcon />}
						value={maxNps > 0 ? maxNps.toFixed(1) : "—"}
						label="Max notes / sec"
					/>
					<span className="ml-auto truncate">{map.mapper}</span>
				</div>
			</div>
		</div>
	);
}

function DifficultyBadge({ difficulty }: { difficulty: BeatmapDifficultyDto }) {
	return (
		<Badge
			variant="outline"
			className={cn(
				"border",
				RANK_STYLES[difficulty.difficultyRank] ??
					"border-border bg-muted text-muted-foreground",
			)}
		>
			{difficulty.difficultyName}
		</Badge>
	);
}

function DifficultyTags({
	difficulties,
}: {
	difficulties: BeatmapDifficultyDto[];
}) {
	const containerRef = useRef<HTMLDivElement>(null);
	const measureRef = useRef<HTMLDivElement>(null);
	const total = difficulties.length;
	const [visibleCount, setVisibleCount] = useState(total);

	const measure = useCallback(() => {
		const container = containerRef.current;
		const measureEl = measureRef.current;
		if (!container || !measureEl) return;

		const children = Array.from(measureEl.children) as HTMLElement[];
		const badges = children.slice(0, total);
		const plusBadge = children[total];
		if (badges.length === 0) return;

		const width = container.clientWidth;
		const lastRight =
			badges[badges.length - 1].offsetLeft +
			badges[badges.length - 1].offsetWidth;

		if (lastRight <= width) {
			setVisibleCount(total);
			return;
		}

		const gap = 4;
		const plusReserve = (plusBadge?.offsetWidth ?? 40) + gap;

		let count = 0;
		for (let i = 0; i < badges.length; i++) {
			const right = badges[i].offsetLeft + badges[i].offsetWidth;
			if (right + plusReserve > width) break;
			count = i + 1;
		}
		setVisibleCount(Math.max(1, count));
	}, [total]);

	useLayoutEffect(() => {
		measure();
		const container = containerRef.current;
		if (!container) return;
		const ro = new ResizeObserver(() => measure());
		ro.observe(container);
		return () => ro.disconnect();
	}, [measure]);

	const visible = difficulties.slice(0, visibleCount);
	const hidden = difficulties.slice(visibleCount);

	return (
		<div ref={containerRef} className="relative">
			<div className="flex flex-nowrap items-center gap-1 overflow-hidden">
				{visible.map((d) => (
					<DifficultyBadge key={d.id} difficulty={d} />
				))}
				{hidden.length > 0 && (
					<Tooltip>
						<TooltipTrigger asChild>
							<button
								type="button"
								className={cn(
									badgeVariants({ variant: "outline" }),
									"border-border bg-muted text-muted-foreground",
								)}
							>
								+{hidden.length}
							</button>
						</TooltipTrigger>
						<TooltipContent className="flex max-w-[16rem] flex-wrap gap-1">
							{hidden.map((d) => (
								<DifficultyBadge key={d.id} difficulty={d} />
							))}
						</TooltipContent>
					</Tooltip>
				)}
			</div>

			<div
				aria-hidden
				ref={measureRef}
				className="pointer-events-none invisible absolute left-0 top-0 flex flex-nowrap items-center gap-1"
			>
				{difficulties.map((d) => (
					<DifficultyBadge key={d.id} difficulty={d} />
				))}
				<span className={badgeVariants({ variant: "outline" })}>+9</span>
			</div>
		</div>
	);
}

function Stat({
	icon,
	value,
	label,
}: {
	icon: React.ReactNode;
	value: string;
	label: string;
}) {
	return (
		<Tooltip>
			<TooltipTrigger asChild>
				<span className="flex cursor-default items-center gap-1 font-mono tabular-nums [&_svg]:size-3.5">
					{icon}
					{value}
				</span>
			</TooltipTrigger>
			<TooltipContent>{label}</TooltipContent>
		</Tooltip>
	);
}

function TruncatedText({
	children,
	className,
}: {
	children: string;
	className?: string;
}) {
	const ref = useRef<HTMLSpanElement>(null);
	const [isTruncated, setIsTruncated] = useState(false);

	const check = useCallback(() => {
		const el = ref.current;
		if (el) setIsTruncated(el.scrollWidth > el.clientWidth);
	}, []);

	useLayoutEffect(() => {
		check();
		const el = ref.current;
		if (!el) return;
		const ro = new ResizeObserver(check);
		ro.observe(el);
		return () => ro.disconnect();
	}, [check]);

	const text = (
		<span ref={ref} className={cn("block truncate", className)}>
			{children}
		</span>
	);

	if (!isTruncated) return text;

	return (
		<Tooltip>
			<TooltipTrigger asChild>{text}</TooltipTrigger>
			<TooltipContent className="max-w-[20rem]">{children}</TooltipContent>
		</Tooltip>
	);
}

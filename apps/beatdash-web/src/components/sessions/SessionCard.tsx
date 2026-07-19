import { Badge } from "@shiron/ui/components/ui/badge";
import { cn } from "@shiron/ui/lib/utils";
import { CpuIcon, MusicNotesIcon } from "@solar-icons/react/dynamic";
import { Link, useSearch } from "@tanstack/react-router";
import { formatDistanceToNow } from "date-fns";
import { useState } from "react";
import { getGetApiMapsMapIdCoverUrl } from "@/api/maps/maps";
import type { PlaySessionListItemDto } from "@/api/model";
import {
	DIFFICULTY_STYLES,
	formatAccuracy,
	formatDuration,
	formatScore,
	outcomeMeta,
	RANK_STYLES,
} from "@/lib/sessions";

export function SessionCard({ session }: { session: PlaySessionListItemDto }) {
	const results = session.results;
	const [coverFailed, setCoverFailed] = useState(false);
	const sessionSearch = useSearch({ from: "/plays/" });
	const diffStyle =
		DIFFICULTY_STYLES[session.difficultyRank] ??
		"border-border bg-muted text-muted-foreground";
	const outcome = outcomeMeta(session.endReason);

	return (
		<Link
			to="/plays/$id"
			params={{ id: session.id }}
			search={sessionSearch}
			className="block"
		>
			<div className="flex items-center gap-3 rounded-xl border border-border bg-card p-3 transition-all duration-200 hover:border-primary/40 hover:bg-accent/30 focus-within:border-primary/40">
				<div className="size-14 shrink-0 overflow-hidden rounded-lg bg-gradient-to-br from-primary/40 to-[oklch(0.62_0.19_255)]/40">
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
							<MusicNotesIcon className="size-5 text-muted-foreground/40" />
						</div>
					)}
				</div>

				<div className="min-w-0 flex-1">
					<div className="flex items-center gap-1.5">
						<span className="truncate font-heading text-sm font-semibold">
							{session.songName}
						</span>
						{session.autoMode && (
							<Badge
								variant="secondary"
								className="shrink-0 gap-0.5 px-1.5 py-0 text-[10px] text-muted-foreground"
							>
								<CpuIcon className="size-2.5" />
								Auto
							</Badge>
						)}
					</div>
					<div className="mt-1 flex items-center gap-1.5">
						<Badge
							variant="outline"
							className={cn("h-5 px-1.5 text-[10px]", diffStyle)}
						>
							{session.difficultyName}
						</Badge>
						{outcome && (
							<Badge
								variant="outline"
								className={cn("h-5 px-1.5 text-[10px]", outcome.className)}
							>
								{outcome.label}
							</Badge>
						)}
						{session.isPersonalBest && (
							<Badge className="h-5 border-amber-500/30 bg-amber-500/15 px-1.5 text-[10px] text-amber-400">
								PB
							</Badge>
						)}
						{results?.fullCombo && (
							<span className="text-[10px] font-bold uppercase tracking-wide text-amber-400">
								FC
							</span>
						)}
						<span className="truncate text-[10px] text-muted-foreground/60">
							{formatDuration(session.duration)} · {session.songAuthor} ·{" "}
							{formatDistanceToNow(new Date(session.startedAt), {
								addSuffix: true,
							})}
						</span>
					</div>
				</div>

				{results ? (
					<div className="flex shrink-0 items-center gap-3 border-l border-border/60 pl-3">
						<span
							className={cn(
								"font-heading text-xl font-bold leading-none",
								RANK_STYLES[results.rank] ?? "text-muted-foreground",
							)}
						>
							{results.rank}
						</span>
						<div className="text-right">
							<div className="font-mono text-sm font-medium tabular-nums">
								{formatScore(results.score)}
							</div>
							<div className="font-mono text-[10px] tabular-nums text-muted-foreground">
								{formatAccuracy(results.accuracy)} · {Number(results.maxCombo)}x
							</div>
						</div>
					</div>
				) : (
					<div className="flex shrink-0 items-center border-l border-border/60 pl-3">
						<span className="text-xs text-muted-foreground">In progress</span>
					</div>
				)}
			</div>
		</Link>
	);
}

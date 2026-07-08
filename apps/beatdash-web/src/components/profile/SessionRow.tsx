import { MusicNotesSimpleIcon } from "@phosphor-icons/react";
import { Badge } from "@shiron/ui/components/ui/badge";
import { cn } from "@shiron/ui/lib/utils";
import { Link } from "@tanstack/react-router";
import { formatDistanceToNow } from "date-fns";
import { useState } from "react";
import { getGetApiMapsMapIdCoverUrl } from "@/api/maps/maps";
import type { PlaySessionListItemDto } from "@/api/model";
import {
	DIFFICULTY_STYLES,
	formatAccuracy,
	formatScore,
	RANK_STYLES,
} from "@/lib/sessions";

/**
 * A single play as a compact row. Links to the session detail when
 * <c>interactive</c> (the default); public profiles render it non-interactive
 * since anonymous visitors can't open a session.
 */
export function SessionRow({
	session,
	interactive = true,
}: {
	session: PlaySessionListItemDto;
	interactive?: boolean;
}) {
	const results = session.results;
	const [coverFailed, setCoverFailed] = useState(false);
	const diffStyle =
		DIFFICULTY_STYLES[session.difficultyRank] ??
		"border-border bg-muted text-muted-foreground";

	const className = cn(
		"flex items-center gap-3 rounded-lg border border-border bg-card p-2",
		interactive &&
			"transition-colors hover:border-primary/40 hover:bg-accent/30",
	);

	const content = (
		<>
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
		</>
	);

	if (!interactive) {
		return <div className={className}>{content}</div>;
	}

	return (
		<Link to="/sessions/$id" params={{ id: session.id }} className={className}>
			{content}
		</Link>
	);
}

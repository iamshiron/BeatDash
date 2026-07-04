import { MusicNotesSimpleIcon } from "@phosphor-icons/react";
import { Badge } from "@shiron/ui/components/ui/badge";
import { Card, CardContent } from "@shiron/ui/components/ui/card";
import { cn } from "@shiron/ui/lib/utils";
import { Link } from "@tanstack/react-router";
import { formatDistanceToNow } from "date-fns";
import { useState } from "react";
import { getGetApiMapsMapIdCoverUrl } from "@/api/maps/maps";
import type { PlaySessionListItemDto } from "@/api/model";
import {
	DIFFICULTY_STYLES,
	formatAccuracy,
	formatDuration,
	formatScore,
	RANK_STYLES,
} from "@/lib/sessions";

export function SessionCard({ session }: { session: PlaySessionListItemDto }) {
	const results = session.results;
	const [coverFailed, setCoverFailed] = useState(false);
	const diffStyle =
		DIFFICULTY_STYLES[session.difficultyRank] ??
		"border-border bg-muted text-muted-foreground";

	return (
		<Link to="/sessions/$id" params={{ id: session.id }} className="block">
			<Card className="transition-colors hover:border-primary/40 focus-within:border-primary/40">
				<CardContent className="flex items-center gap-3 p-3">
					<div className="flex size-12 shrink-0 items-center justify-center overflow-hidden rounded-md bg-gradient-to-br from-primary/40 to-[oklch(0.62_0.19_255)]/40">
						{!coverFailed ? (
							<img
								src={getGetApiMapsMapIdCoverUrl(session.beatmapId)}
								alt={session.songName}
								loading="lazy"
								onError={() => setCoverFailed(true)}
								className="size-full object-cover"
							/>
						) : (
							<MusicNotesSimpleIcon className="size-5 text-muted-foreground/60" />
						)}
					</div>

					<div className="min-w-0 flex-1">
						<div className="flex items-center gap-2">
							<span className="truncate font-heading text-sm font-semibold">
								{session.songName}
							</span>
							<Badge variant="outline" className={cn("shrink-0", diffStyle)}>
								{session.difficultyName}
							</Badge>
							{session.autoMode && (
								<Badge variant="secondary" className="shrink-0">
									Auto
								</Badge>
							)}
						</div>
						<p className="truncate text-xs text-muted-foreground">
							by {session.songAuthor} · mapped by {session.mapper}
						</p>
					</div>

					<div className="shrink-0 text-right">
						{results ? (
							<>
								<div className="flex items-center justify-end gap-2">
									{results.fullCombo && (
										<span className="text-xs font-semibold text-amber-400">
											FC
										</span>
									)}
									<span
										className={cn(
											"text-lg font-bold",
											RANK_STYLES[results.rank] ?? "text-muted-foreground",
										)}
									>
										{results.rank}
									</span>
									<span className="font-mono text-sm tabular-nums">
										{formatScore(results.score)}
									</span>
								</div>
								<div className="font-mono text-xs tabular-nums text-muted-foreground">
									{formatAccuracy(results.accuracy)} ·{" "}
									{Number(results.maxCombo)}x ·{" "}
									{formatDuration(session.duration)}
								</div>
							</>
						) : (
							<span className="text-xs text-muted-foreground">In progress</span>
						)}
						<div className="text-xs text-muted-foreground/60">
							{formatDistanceToNow(new Date(session.startedAt), {
								addSuffix: true,
							})}
						</div>
					</div>
				</CardContent>
			</Card>
		</Link>
	);
}

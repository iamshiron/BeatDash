import { MusicNotesIcon } from "@solar-icons/react/dynamic";
import { Link } from "@tanstack/react-router";
import { useState } from "react";
import { getGetApiMapsMapIdCoverUrl } from "@/api/maps/maps";
import type { MostPlayedMapDto } from "@/api/model";
import { formatScore } from "@/lib/sessions";

/**
 * A most-played map as a compact row. Links to the map detail when
 * <c>interactive</c> (the default); non-interactive on public profiles.
 */
export function MostPlayedRow({
	map,
	interactive = true,
}: {
	map: MostPlayedMapDto;
	interactive?: boolean;
}) {
	const [coverFailed, setCoverFailed] = useState(false);

	const className =
		"flex min-w-0 items-center gap-3 rounded-lg border border-border bg-card p-2" +
		(interactive
			? " transition-colors hover:border-primary/40 hover:bg-accent/30"
			: "");

	const content = (
		<>
			<div className="size-10 shrink-0 overflow-hidden rounded-md bg-gradient-to-br from-primary/40 to-[oklch(0.62_0.19_255)]/40">
				{!coverFailed ? (
					<img
						src={getGetApiMapsMapIdCoverUrl(map.beatmapId)}
						alt={map.songName}
						loading="lazy"
						onError={() => setCoverFailed(true)}
						className="size-full object-cover"
					/>
				) : (
					<div className="flex size-full items-center justify-center">
						<MusicNotesIcon className="size-4 text-muted-foreground/40" />
					</div>
				)}
			</div>
			<div className="min-w-0 flex-1">
				<span className="block truncate font-heading text-sm font-semibold">
					{map.songName}
				</span>
				<span className="block truncate text-[10px] text-muted-foreground/60">
					{map.songAuthor} · {map.mapper}
				</span>
			</div>
			<span className="shrink-0 font-mono text-xs tabular-nums text-muted-foreground">
				{formatScore(Number(map.playCount))}×
			</span>
		</>
	);

	if (!interactive) {
		return <div className={className}>{content}</div>;
	}

	return (
		<Link to="/maps/$id" params={{ id: map.beatmapId }} className={className}>
			{content}
		</Link>
	);
}

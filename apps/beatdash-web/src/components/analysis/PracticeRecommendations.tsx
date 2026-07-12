import { Badge } from "@shiron/ui/components/ui/badge";
import {
	Card,
	CardContent,
	CardDescription,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import { MusicNotes, Target } from "@solar-icons/react";
import { Link } from "@tanstack/react-router";
import { useState } from "react";
import { getGetApiMapsMapIdCoverUrl } from "@/api/maps/maps";
import type { PracticeRecommendationDto } from "@/api/model";

function RecommendationRow({ rec }: { rec: PracticeRecommendationDto }) {
	const [coverFailed, setCoverFailed] = useState(false);
	return (
		<Link
			to="/maps/$id"
			params={{ id: rec.beatmapId }}
			className="flex items-center gap-3 rounded-lg border border-border bg-card p-2 transition-colors hover:border-primary/40 hover:bg-accent/30"
		>
			<div className="size-10 shrink-0 overflow-hidden rounded-md bg-gradient-to-br from-primary/40 to-[oklch(0.62_0.19_255)]/40">
				{!coverFailed ? (
					<img
						src={getGetApiMapsMapIdCoverUrl(rec.beatmapId)}
						alt={rec.songName}
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
				<span className="block truncate font-heading text-sm font-semibold">
					{rec.songName}
				</span>
				<span className="block truncate text-[10px] text-muted-foreground/60">
					{rec.songAuthor} · {rec.mapper} · {rec.difficultyName}
				</span>
			</div>
			<div className="flex shrink-0 flex-wrap justify-end gap-1">
				{rec.targetedCharacteristics.map((c) => (
					<Badge key={c} variant="secondary" className="gap-1 capitalize">
						<Target className="size-3" />
						{c}
					</Badge>
				))}
			</div>
		</Link>
	);
}

/**
 * Maps to practice next, chosen to target the player's weak characteristics
 * within an attainable difficulty band. Hidden when there's nothing to suggest.
 */
export function PracticeRecommendations({
	recommendations,
}: {
	recommendations: PracticeRecommendationDto[] | null | undefined;
}) {
	if (!recommendations || recommendations.length === 0) return null;

	return (
		<Card>
			<CardHeader>
				<CardTitle>Practice recommendations</CardTitle>
				<CardDescription>
					Maps that target your weaker play styles at a reachable difficulty.
				</CardDescription>
			</CardHeader>
			<CardContent className="flex flex-col gap-2">
				{recommendations.map((rec) => (
					<RecommendationRow key={rec.beatmapDifficultyId} rec={rec} />
				))}
			</CardContent>
		</Card>
	);
}

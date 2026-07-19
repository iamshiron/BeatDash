import { Badge } from "@shiron/ui/components/ui/badge";
import { cn } from "@shiron/ui/lib/utils";
import {
	AltArrowDownIcon,
	FireIcon,
	MedalStarIcon,
	StopwatchIcon,
	TargetIcon,
} from "@solar-icons/react/dynamic";
import { useState } from "react";
import type { SessionSummaryDto } from "@/api/model";
import { SessionRow } from "@/components/profile/SessionRow";
import { formatAccuracy, RANK_STYLES } from "@/lib/sessions";

function formatPlayTime(ms: number): string {
	const totalMinutes = Math.round(ms / 60000);
	const hours = Math.floor(totalMinutes / 60);
	const minutes = totalMinutes % 60;
	return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`;
}

function HeaderStat({
	icon,
	label,
	value,
}: {
	icon: React.ReactNode;
	label: string;
	value: string;
}) {
	return (
		<div className="flex flex-col items-end">
			<span className="font-heading text-sm font-bold tabular-nums">
				{value}
			</span>
			<span className="flex items-center gap-1 text-[10px] text-muted-foreground">
				{icon}
				{label}
			</span>
		</div>
	);
}

/**
 * One sitting (a cluster of plays) as a collapsible card: a summary header that
 * expands to reveal every play in the session.
 */
export function SittingCard({ sitting }: { sitting: SessionSummaryDto }) {
	const [open, setOpen] = useState(false);
	const start = new Date(sitting.startedAt);
	const dateLabel = start.toLocaleDateString(undefined, {
		weekday: "short",
		month: "short",
		day: "numeric",
	});
	const timeLabel = start.toLocaleTimeString(undefined, {
		hour: "numeric",
		minute: "2-digit",
	});

	const playCount = Number(sitting.playCount);
	const kcal =
		sitting.caloriesKcal != null
			? Math.round(Number(sitting.caloriesKcal))
			: null;
	const personalBests = Number(sitting.personalBests);

	return (
		<div className="overflow-hidden rounded-xl border border-border bg-card">
			<button
				type="button"
				onClick={() => setOpen((o) => !o)}
				aria-expanded={open}
				className="flex w-full items-center gap-3 p-3 text-left transition-colors hover:bg-accent/30"
			>
				<AltArrowDownIcon
					className={cn(
						"size-4 shrink-0 text-muted-foreground transition-transform",
						open && "rotate-180",
					)}
				/>
				<div className="min-w-0 flex-1">
					<div className="font-heading text-sm font-semibold">
						{dateLabel}
						<span className="ml-2 font-normal text-muted-foreground">
							{timeLabel}
						</span>
					</div>
					<div className="text-xs text-muted-foreground">
						{playCount} {playCount === 1 ? "play" : "plays"} ·{" "}
						{formatPlayTime(Number(sitting.totalPlayTimeMs))}
					</div>
				</div>

				<div className="flex shrink-0 items-center gap-4">
					{personalBests > 0 && (
						<Badge className="hidden gap-1 border-amber-500/30 bg-amber-500/15 text-amber-400 sm:inline-flex">
							<MedalStarIcon className="size-3" />
							{personalBests} PB
						</Badge>
					)}
					<HeaderStat
						icon={<TargetIcon className="size-3" />}
						label="Accuracy"
						value={formatAccuracy(Number(sitting.avgAccuracy))}
					/>
					{kcal != null && (
						<HeaderStat
							icon={<FireIcon className="size-3" />}
							label="Calories"
							value={`${kcal}`}
						/>
					)}
					<HeaderStat
						icon={<StopwatchIcon className="size-3" />}
						label="Active"
						value={formatPlayTime(Number(sitting.totalPlayTimeMs))}
					/>
				</div>
			</button>

			{open && (
				<div className="flex flex-col gap-1.5 border-t border-border bg-card/50 p-3">
					<div className="mb-1 flex flex-wrap items-center gap-1.5">
						{sitting.rankDistribution.map((r) => (
							<span
								key={r.rank}
								className="inline-flex items-center gap-1 rounded-md bg-muted/40 px-1.5 py-0.5"
							>
								<span
									className={cn(
										"font-heading text-xs font-bold",
										RANK_STYLES[r.rank] ?? "text-muted-foreground",
									)}
								>
									{r.rank}
								</span>
								<span className="font-mono text-[10px] tabular-nums text-muted-foreground">
									{Number(r.count)}
								</span>
							</span>
						))}
					</div>
					{sitting.plays.map((play) => (
						<SessionRow key={play.id} session={play} />
					))}
				</div>
			)}
		</div>
	);
}

import { Badge } from "@shiron/ui/components/ui/badge";
import { Card, CardContent } from "@shiron/ui/components/ui/card";
import { cn } from "@shiron/ui/lib/utils";
import { CupIcon, MedalStarIcon } from "@solar-icons/react/dynamic";
import type { RecapDeltaDto, SessionRecapDto } from "@/api/model";
import {
	type StatDelta,
	StatDeltaIndicator,
} from "@/components/profile/StatTile";
import { formatAccuracy, formatScore, RANK_STYLES } from "@/lib/sessions";

function signed(n: number): string {
	return `${n >= 0 ? "+" : ""}${n}`;
}

function DeltaChip({ label, delta }: { label: string; delta: StatDelta }) {
	return (
		<div className="flex flex-col items-center gap-0.5">
			<span className="text-[10px] text-muted-foreground">{label}</span>
			<StatDeltaIndicator delta={delta} />
		</div>
	);
}

function deltasFrom(d: RecapDeltaDto): { label: string; delta: StatDelta }[] {
	const acc = Number(d.accuracyDelta);
	const score = Number(d.scoreDelta);
	const misses = Number(d.missesDelta);
	const dir = (n: number): StatDelta["direction"] =>
		n > 0 ? "up" : n < 0 ? "down" : "neutral";
	return [
		{
			label: "Accuracy",
			delta: {
				value: `${acc >= 0 ? "+" : ""}${(acc * 100).toFixed(2)}%`,
				direction: dir(acc),
				good: acc > 0,
			},
		},
		{
			label: "Score",
			delta: {
				value: signed(score),
				direction: dir(score),
				good: score > 0,
			},
		},
		{
			label: "Misses",
			delta: {
				value: signed(misses),
				direction: dir(misses),
				good: misses < 0,
			},
		},
	];
}

/**
 * Post-session recap surface: headline result plus how it compares to the
 * previous attempt and the player's average on this difficulty.
 */
export function SessionRecap({ recap }: { recap: SessionRecapDto }) {
	const results = recap.session.results;
	if (!results) return null;

	const hasPrevious = recap.previousAttempt != null;

	return (
		<Card className="mb-4 border-primary/30 bg-gradient-to-br from-primary/10 to-transparent">
			<CardContent className="flex flex-col gap-4 py-5">
				<div className="flex items-center justify-between gap-4">
					<div className="flex items-center gap-2">
						<span className="font-heading text-sm font-semibold text-muted-foreground">
							Play summary
						</span>
						{recap.isNewPersonalBest && (
							<Badge className="gap-1 border-amber-500/30 bg-amber-500/15 text-amber-400">
								<MedalStarIcon className="size-3.5" />
								New personal best
							</Badge>
						)}
						{results.fullCombo && (
							<Badge variant="secondary" className="gap-1 text-amber-400">
								<CupIcon className="size-3.5" />
								Full combo
							</Badge>
						)}
					</div>
					<div className="flex items-baseline gap-3">
						<span
							className={cn(
								"font-heading text-2xl font-bold",
								RANK_STYLES[results.rank] ?? "text-muted-foreground",
							)}
						>
							{results.rank}
						</span>
						<span className="font-heading text-2xl font-bold tabular-nums">
							{formatScore(results.score)}
						</span>
						<span className="text-sm tabular-nums text-muted-foreground">
							{formatAccuracy(results.accuracy)}
						</span>
					</div>
				</div>

				<div className="grid grid-cols-2 gap-4">
					<div className="flex flex-col gap-2 rounded-lg border border-border/40 p-3">
						<span className="text-xs font-medium">
							{hasPrevious
								? "vs previous attempt"
								: "First attempt on this map"}
						</span>
						{hasPrevious && (
							<div className="flex justify-around">
								{deltasFrom(recap.vsPrevious).map((d) => (
									<DeltaChip key={d.label} label={d.label} delta={d.delta} />
								))}
							</div>
						)}
					</div>
					<div className="flex flex-col gap-2 rounded-lg border border-border/40 p-3">
						<span className="text-xs font-medium">vs your average</span>
						<div className="flex justify-around">
							{deltasFrom(recap.vsAverage).map((d) => (
								<DeltaChip key={d.label} label={d.label} delta={d.delta} />
							))}
						</div>
					</div>
				</div>
			</CardContent>
		</Card>
	);
}

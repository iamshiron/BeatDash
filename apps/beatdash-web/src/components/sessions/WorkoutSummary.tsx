import { Badge } from "@shiron/ui/components/ui/badge";
import {
	Card,
	CardContent,
	CardDescription,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import { cn } from "@shiron/ui/lib/utils";
import { HeartPulseIcon } from "@solar-icons/react/dynamic";
import { useGetSessionWorkout } from "@/api/health/health";
import { useAuth } from "@/contexts/auth";

function Metric({ label, value }: { label: string; value: string }) {
	return (
		<div className="flex flex-col items-center justify-center rounded-lg border border-border/40 bg-card/50 py-3">
			<span className="font-mono text-lg font-semibold tabular-nums">
				{value}
			</span>
			<span className="text-center text-[11px] text-muted-foreground">
				{label}
			</span>
		</div>
	);
}

const CONFIDENCE: Record<string, { label: string; className: string }> = {
	hr: {
		label: "Heart rate",
		className: "border-rose-500/30 bg-rose-500/15 text-rose-400",
	},
	motion: {
		label: "Measured",
		className: "border-emerald-500/30 bg-emerald-500/15 text-emerald-400",
	},
	estimated: {
		label: "Estimated",
		className: "border-border bg-muted text-muted-foreground",
	},
};

function intensityLabel(v: number): string {
	if (v < 0.4) return "Light";
	if (v < 0.7) return "Moderate";
	return "Vigorous";
}

/**
 * Per-play fitness figures — calories, active time, intensity, movement and (when a
 * wearable pushed it) heart rate. Only fetches/renders when the viewer has health
 * tracking on and the play has a workout, so it stays invisible otherwise.
 */
export function WorkoutSummary({ sessionId }: { sessionId: string }) {
	const { user } = useAuth();
	const enabled = Boolean(user?.healthTrackingEnabled);
	const query = useGetSessionWorkout(sessionId, { query: { enabled } });
	const workout =
		enabled && query.data?.status === 200 ? query.data.data : null;
	if (!workout) return null;

	const conf = CONFIDENCE[workout.confidence] ?? CONFIDENCE.estimated;
	const kcal = Math.round(Number(workout.kcal));
	const minutes = Number(workout.activeMinutes);
	const distance =
		Number(workout.leftDistanceM) + Number(workout.rightDistanceM);
	const avgHr =
		workout.avgHeartRate != null ? Number(workout.avgHeartRate) : null;
	const maxHr =
		workout.maxHeartRate != null ? Number(workout.maxHeartRate) : null;

	return (
		<Card className="mb-4">
			<CardHeader>
				<div className="flex items-center justify-between gap-2">
					<CardTitle>Workout</CardTitle>
					<Badge variant="outline" className={cn("border", conf.className)}>
						{conf.label}
					</Badge>
				</div>
				<CardDescription>
					Estimated energy and movement for this play.
				</CardDescription>
			</CardHeader>
			<CardContent>
				<div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
					<Metric label="Calories" value={`${kcal} kcal`} />
					<Metric label="Active time" value={`${minutes.toFixed(1)} min`} />
					<Metric
						label="Intensity"
						value={intensityLabel(Number(workout.intensity))}
					/>
					<Metric label="Distance moved" value={`${distance.toFixed(0)} m`} />
					{avgHr != null && (
						<Metric label="Avg heart rate" value={`${Math.round(avgHr)} bpm`} />
					)}
					{maxHr != null && (
						<Metric label="Max heart rate" value={`${maxHr} bpm`} />
					)}
				</div>
				{avgHr == null && (
					<p className="mt-3 flex items-center gap-1.5 text-[11px] text-muted-foreground">
						<HeartPulseIcon className="size-3.5" />
						Connect a smartwatch in Settings for heart-rate-based accuracy.
					</p>
				)}
			</CardContent>
		</Card>
	);
}

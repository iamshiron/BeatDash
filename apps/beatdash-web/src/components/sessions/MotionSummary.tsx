import {
	Card,
	CardContent,
	CardDescription,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import { useGetApiSessionsIdMotionSummary } from "@/api/sessions/sessions";
import { FatigueCurve } from "@/components/sessions/FatigueCurve";

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

const meters = (v: number) => `${v.toFixed(1)} m`;

/**
 * Motion-derived analytics for a play: saber travel, reach range, dodge/head
 * movement, and a fatigue curve. Renders nothing until the summary is available.
 */
export function MotionSummary({ sessionId }: { sessionId: string }) {
	const query = useGetApiSessionsIdMotionSummary(sessionId);
	const motion = query.data?.status === 200 ? query.data.data : null;
	if (!motion) return null;

	return (
		<Card className="mb-4">
			<CardHeader>
				<CardTitle>Motion & Fatigue</CardTitle>
				<CardDescription>
					Movement derived from {Number(motion.frameCount).toLocaleString()}{" "}
					tracked frames.
				</CardDescription>
			</CardHeader>
			<CardContent className="flex flex-col gap-4">
				<div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
					<Metric
						label="Left saber travel"
						value={meters(Number(motion.leftSaberTravel))}
					/>
					<Metric
						label="Right saber travel"
						value={meters(Number(motion.rightSaberTravel))}
					/>
					<Metric
						label="Left reach"
						value={meters(Number(motion.leftReachRange))}
					/>
					<Metric
						label="Right reach"
						value={meters(Number(motion.rightReachRange))}
					/>
					<Metric
						label="Avg left speed"
						value={`${Number(motion.avgLeftSaberSpeed).toFixed(1)} m/s`}
					/>
					<Metric
						label="Avg right speed"
						value={`${Number(motion.avgRightSaberSpeed).toFixed(1)} m/s`}
					/>
					<Metric
						label="Head movement"
						value={meters(Number(motion.headTravel))}
					/>
					<Metric label="Head range" value={meters(Number(motion.headRange))} />
				</div>
				<FatigueCurve points={motion.fatigueCurve} />
			</CardContent>
		</Card>
	);
}

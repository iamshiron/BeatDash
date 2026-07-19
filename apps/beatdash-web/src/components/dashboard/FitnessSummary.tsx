import { Button } from "@shiron/ui/components/ui/button";
import {
	Card,
	CardContent,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import { FireIcon, RunningIcon } from "@solar-icons/react/dynamic";
import { Link } from "@tanstack/react-router";
import { useGetHealthOverview } from "@/api/health/health";
import { AnimatedNumber } from "@/components/common/AnimatedNumber";
import { useAuth } from "@/contexts/auth";

function Tile({
	icon,
	label,
	children,
}: {
	icon: React.ReactNode;
	label: string;
	children: React.ReactNode;
}) {
	return (
		<div className="flex flex-col gap-1 rounded-xl border border-border bg-card/50 p-3">
			<span className="flex items-center gap-1.5 text-[11px] font-medium uppercase tracking-wide text-muted-foreground">
				{icon}
				{label}
			</span>
			<span className="font-heading text-xl font-bold tabular-nums">
				{children}
			</span>
		</div>
	);
}

/**
 * A compact fitness card for the dashboard. Self-hides unless the viewer has health
 * tracking on and has data — competitive players never see it. Links to the full page.
 */
export function FitnessSummary() {
	const { user } = useAuth();
	const enabled = Boolean(user?.healthTrackingEnabled);
	const query = useGetHealthOverview({ query: { enabled } });
	const overview =
		enabled && query.data?.status === 200 ? query.data.data : null;
	if (!overview || Number(overview.totalPlays) === 0) return null;

	return (
		<Card>
			<CardHeader className="flex-row items-center justify-between gap-2 space-y-0">
				<CardTitle>Fitness</CardTitle>
				<Button variant="ghost" size="sm" asChild>
					<Link to="/health">View details</Link>
				</Button>
			</CardHeader>
			<CardContent>
				<div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
					<Tile
						icon={<FireIcon className="size-3.5" />}
						label="Calories this week"
					>
						<AnimatedNumber
							value={Number(overview.weekKcal)}
							format={(n) => `${Math.round(n).toLocaleString("en-US")}`}
						/>
					</Tile>
					<Tile
						icon={<RunningIcon className="size-3.5" />}
						label="Active min this week"
					>
						<AnimatedNumber
							value={Number(overview.weekActiveMinutes)}
							format={(n) => `${Math.round(n)}`}
						/>
					</Tile>
					<Tile icon={<FireIcon className="size-3.5" />} label="Calories today">
						<AnimatedNumber
							value={Number(overview.todayKcal)}
							format={(n) => `${Math.round(n).toLocaleString("en-US")}`}
						/>
					</Tile>
					<Tile
						icon={<FireIcon className="size-3.5" />}
						label="Career calories"
					>
						<AnimatedNumber
							value={Number(overview.careerKcal)}
							format={(n) => `${Math.round(n).toLocaleString("en-US")}`}
						/>
					</Tile>
				</div>
			</CardContent>
		</Card>
	);
}

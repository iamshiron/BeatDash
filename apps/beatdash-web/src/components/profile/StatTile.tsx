import { Card, CardContent } from "@shiron/ui/components/ui/card";
import { cn } from "@shiron/ui/lib/utils";
import { AltArrowDown, AltArrowUp } from "@solar-icons/react";

/** A change indicator for a stat tile. `good` colours the delta regardless of arrow direction. */
export interface StatDelta {
	value: string;
	direction: "up" | "down" | "neutral";
	/** Whether this change is an improvement (green) or not (rose). Omit for neutral. */
	good?: boolean;
}

/** A compact labelled metric tile used on the dashboard and public profiles. */
export function StatTile({
	icon,
	label,
	value,
	sub,
	delta,
}: {
	icon?: React.ReactNode;
	label: string;
	value: string;
	sub?: string;
	delta?: StatDelta;
}) {
	return (
		<Card size="sm">
			<CardContent className="flex flex-col gap-1 py-4">
				<span className="flex items-center gap-1.5 text-xs text-muted-foreground">
					{icon}
					{label}
				</span>
				<span className="font-heading text-2xl font-bold tabular-nums">
					{value}
				</span>
				{delta && <StatDeltaIndicator delta={delta} />}
				{sub && (
					<span className="text-[10px] text-muted-foreground">{sub}</span>
				)}
			</CardContent>
		</Card>
	);
}

/** Inline up/down delta chip, coloured by whether the change is good (not by arrow). */
export function StatDeltaIndicator({ delta }: { delta: StatDelta }) {
	const color =
		delta.direction === "neutral"
			? "text-muted-foreground"
			: delta.good
				? "text-emerald-400"
				: "text-rose-400";
	const Arrow = delta.direction === "down" ? AltArrowDown : AltArrowUp;
	return (
		<span
			className={cn("flex items-center gap-0.5 text-[11px] font-medium", color)}
		>
			{delta.direction !== "neutral" && <Arrow className="size-3" />}
			<span className="font-mono tabular-nums">{delta.value}</span>
		</span>
	);
}

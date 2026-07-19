import { Card, CardContent } from "@shiron/ui/components/ui/card";
import {
	Stat,
	StatDelta as StatDeltaChip,
	StatLabel,
	StatValue,
} from "@shiron/ui/components/ui/stat";
import { AltArrowDownIcon, AltArrowUpIcon } from "@solar-icons/react/dynamic";

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
	value: React.ReactNode;
	sub?: string;
	delta?: StatDelta;
}) {
	return (
		<Card size="sm">
			<CardContent className="py-4">
				<Stat>
					<StatLabel className="flex items-center gap-1.5 normal-case">
						{icon}
						{label}
					</StatLabel>
					<StatValue>{value}</StatValue>
					{delta && <StatDeltaIndicator delta={delta} />}
					{sub && (
						<span className="text-[10px] text-muted-foreground">{sub}</span>
					)}
				</Stat>
			</CardContent>
		</Card>
	);
}

/** Inline up/down delta chip, coloured by whether the change is good (not by arrow). */
export function StatDeltaIndicator({ delta }: { delta: StatDelta }) {
	// The library chip colours by trend, so map "good" onto the green (up) /
	// red (down) trend while the arrow still follows the real direction.
	const trend =
		delta.direction === "neutral" ? "neutral" : delta.good ? "up" : "down";
	const Arrow = delta.direction === "down" ? AltArrowDownIcon : AltArrowUpIcon;
	return (
		<StatDeltaChip trend={trend}>
			{delta.direction !== "neutral" && <Arrow />}
			<span className="font-mono tabular-nums">{delta.value}</span>
		</StatDeltaChip>
	);
}

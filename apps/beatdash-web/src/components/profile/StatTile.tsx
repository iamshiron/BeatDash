import { Card, CardContent } from "@shiron/ui/components/ui/card";

/** A compact labelled metric tile used on the dashboard and public profiles. */
export function StatTile({
	icon,
	label,
	value,
	sub,
}: {
	icon?: React.ReactNode;
	label: string;
	value: string;
	sub?: string;
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
				{sub && (
					<span className="text-[10px] text-muted-foreground">{sub}</span>
				)}
			</CardContent>
		</Card>
	);
}

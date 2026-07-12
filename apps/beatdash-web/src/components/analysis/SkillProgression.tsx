import {
	Card,
	CardContent,
	CardDescription,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import {
	ChartContainer,
	ChartLegend,
	ChartLegendContent,
	ChartTooltip,
	ChartTooltipContent,
} from "@shiron/ui/components/ui/chart";
import { useMemo } from "react";
import { CartesianGrid, Line, LineChart, XAxis, YAxis } from "recharts";
import type { SkillProgressionDto } from "@/api/model";
import { SKILL_AXES, SKILL_CHART_CONFIG } from "@/lib/charts";

function formatWeek(weekStart: string): string {
	const [y, m, d] = weekStart.split("-").map(Number);
	return new Date(y, m - 1, d).toLocaleDateString(undefined, {
		month: "short",
		day: "numeric",
	});
}

/**
 * Weekly skill-per-characteristic time series — one line per characteristic on a
 * single 0–1 axis. Renders nothing until at least one week has analyzed plays.
 */
export function SkillProgression({
	data,
}: {
	data: SkillProgressionDto | null | undefined;
}) {
	const chartData = useMemo(() => {
		if (!data) return [];
		return data.weeks.map((week) => {
			const byKey = new Map(week.characteristics.map((c) => [c.key, c]));
			const row: Record<string, number | string | null> = {
				week: formatWeek(week.weekStart),
			};
			for (const axis of SKILL_AXES) {
				const entry = byKey.get(axis.key);
				row[axis.key] = entry ? Number(entry.skill) : null;
			}
			return row;
		});
	}, [data]);

	const hasData = useMemo(
		() =>
			chartData.some((row) =>
				SKILL_AXES.some((a) => typeof row[a.key] === "number"),
			),
		[chartData],
	);

	if (!data || !hasData) return null;

	return (
		<Card>
			<CardHeader>
				<CardTitle>Skill progression</CardTitle>
				<CardDescription>
					Weekly skill per play style, weighted by accuracy.
				</CardDescription>
			</CardHeader>
			<CardContent>
				<ChartContainer config={SKILL_CHART_CONFIG} className="h-56 w-full">
					<LineChart
						data={chartData}
						margin={{ left: 4, right: 4, top: 8, bottom: 4 }}
					>
						<CartesianGrid strokeDasharray="3 3" vertical={false} />
						<XAxis
							dataKey="week"
							tickLine={false}
							axisLine={false}
							tickMargin={8}
							minTickGap={16}
						/>
						<YAxis
							domain={[0, 1]}
							tickFormatter={(v: number) => `${Math.round(v * 100)}%`}
							tickLine={false}
							axisLine={false}
							width={36}
						/>
						<ChartTooltip
							content={
								<ChartTooltipContent
									formatter={(value, name) => (
										<span className="flex w-full items-center justify-between gap-2">
											<span className="capitalize text-muted-foreground">
												{name}
											</span>
											<span className="font-mono tabular-nums">
												{(Number(value) * 100).toFixed(0)}%
											</span>
										</span>
									)}
								/>
							}
						/>
						{SKILL_AXES.map((axis) => (
							<Line
								key={axis.key}
								dataKey={axis.key}
								stroke={`var(--color-${axis.key})`}
								strokeWidth={2}
								dot={false}
								type="monotone"
								connectNulls
							/>
						))}
						<ChartLegend content={<ChartLegendContent />} />
					</LineChart>
				</ChartContainer>
			</CardContent>
		</Card>
	);
}

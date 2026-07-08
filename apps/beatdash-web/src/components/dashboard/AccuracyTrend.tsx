import {
	Card,
	CardContent,
	CardDescription,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import {
	type ChartConfig,
	ChartContainer,
	ChartTooltip,
	ChartTooltipContent,
} from "@shiron/ui/components/ui/chart";
import { useMemo } from "react";
import {
	Area,
	Bar,
	CartesianGrid,
	ComposedChart,
	XAxis,
	YAxis,
} from "recharts";
import { useGetApiSessionsTrends } from "@/api/sessions/sessions";

const chartConfig = {
	accuracy: { label: "Accuracy", color: "oklch(0.62 0.19 255)" },
	plays: { label: "Plays", color: "oklch(0.72 0.17 152)" },
} satisfies ChartConfig;

function formatWeek(weekStart: string): string {
	// weekStart is a YYYY-MM-DD date; render as "MMM d" without TZ drift.
	const [y, m, d] = weekStart.split("-").map(Number);
	return new Date(y, m - 1, d).toLocaleDateString(undefined, {
		month: "short",
		day: "numeric",
	});
}

export function AccuracyTrend() {
	const query = useGetApiSessionsTrends();
	const data = query.data?.status === 200 ? query.data.data : null;

	const chartData = useMemo(() => {
		if (!data) return [];
		return data.map((b) => ({
			week: formatWeek(b.weekStart),
			plays: Number(b.plays),
			accuracy: b.avgAccuracy == null ? null : Number(b.avgAccuracy) * 100,
		}));
	}, [data]);

	const totalPlays = useMemo(
		() => chartData.reduce((sum, b) => sum + b.plays, 0),
		[chartData],
	);

	if (!data || totalPlays === 0) return null;

	return (
		<Card>
			<CardHeader>
				<CardTitle>Accuracy trend</CardTitle>
				<CardDescription>
					Weekly average accuracy and play volume over the last 12 weeks.
				</CardDescription>
			</CardHeader>
			<CardContent>
				<ChartContainer config={chartConfig} className="h-48 w-full">
					<ComposedChart
						data={chartData}
						margin={{ left: 4, right: 4, top: 8, bottom: 4 }}
					>
						<defs>
							<linearGradient id="accuracyFill" x1="0" y1="0" x2="0" y2="1">
								<stop
									offset="0%"
									stopColor="var(--color-accuracy)"
									stopOpacity={0.3}
								/>
								<stop
									offset="100%"
									stopColor="var(--color-accuracy)"
									stopOpacity={0.03}
								/>
							</linearGradient>
						</defs>
						<CartesianGrid strokeDasharray="3 3" vertical={false} />
						<XAxis
							dataKey="week"
							tickLine={false}
							axisLine={false}
							tickMargin={8}
							minTickGap={16}
						/>
						<YAxis
							yAxisId="accuracy"
							domain={[0, 100]}
							tickFormatter={(v: number) => `${v}%`}
							tickLine={false}
							axisLine={false}
							width={36}
						/>
						<YAxis
							yAxisId="plays"
							orientation="right"
							tickLine={false}
							axisLine={false}
							width={28}
							allowDecimals={false}
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
												{name === "accuracy"
													? `${Number(value).toFixed(1)}%`
													: Number(value)}
											</span>
										</span>
									)}
								/>
							}
						/>
						<Bar
							yAxisId="plays"
							dataKey="plays"
							fill="var(--color-plays)"
							fillOpacity={0.25}
							radius={[2, 2, 0, 0]}
						/>
						<Area
							yAxisId="accuracy"
							dataKey="accuracy"
							stroke="var(--color-accuracy)"
							strokeWidth={2}
							fill="url(#accuracyFill)"
							type="monotone"
							connectNulls
						/>
					</ComposedChart>
				</ChartContainer>
			</CardContent>
		</Card>
	);
}

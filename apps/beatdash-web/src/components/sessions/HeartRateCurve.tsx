import {
	ChartContainer,
	ChartTooltip,
	ChartTooltipContent,
} from "@shiron/ui/components/ui/chart";
import { useMemo } from "react";
import { CartesianGrid, Line, LineChart, XAxis, YAxis } from "recharts";
import type { HeartRatePointDto } from "@/api/model";
import { formatSongTimeMs } from "@/lib/sessions";

const chartConfig = {
	bpm: { label: "Heart rate", color: "oklch(0.62 0.24 27)" },
} as const;

/**
 * Wearable heart rate across the play, plotted against elapsed song time. Only
 * meaningful once a smartwatch has pushed samples, so callers render it
 * conditionally — there is no empty state.
 */
export function HeartRateCurve({ points }: { points: HeartRatePointDto[] }) {
	const data = useMemo(
		() =>
			points.map((p) => ({
				time: Number(p.secondsIntoPlay),
				bpm: Number(p.bpm),
			})),
		[points],
	);

	if (data.length < 2) return null;

	return (
		<ChartContainer config={chartConfig} className="h-40 w-full">
			<LineChart data={data} margin={{ left: 4, right: 4, top: 8, bottom: 4 }}>
				<CartesianGrid strokeDasharray="3 3" vertical={false} />
				<XAxis
					dataKey="time"
					tickFormatter={(v: number) => formatSongTimeMs(v * 1000)}
					tickLine={false}
					axisLine={false}
					tickMargin={8}
				/>
				<YAxis
					tickFormatter={(v: number) => `${Math.round(v)}`}
					tickLine={false}
					axisLine={false}
					width={36}
					domain={["dataMin - 10", "dataMax + 10"]}
				/>
				<ChartTooltip
					content={
						<ChartTooltipContent
							labelFormatter={(_, payload) => {
								const time = payload?.[0]?.payload?.time;
								return time != null ? formatSongTimeMs(time * 1000) : "";
							}}
							formatter={(value) => (
								<span className="flex w-full items-center justify-between gap-2">
									<span className="text-muted-foreground">Heart rate</span>
									<span className="font-mono tabular-nums">
										{Number(value).toFixed(0)} bpm
									</span>
								</span>
							)}
						/>
					}
				/>
				<Line
					dataKey="bpm"
					stroke="var(--color-bpm)"
					strokeWidth={2}
					dot={false}
					type="monotone"
				/>
			</LineChart>
		</ChartContainer>
	);
}

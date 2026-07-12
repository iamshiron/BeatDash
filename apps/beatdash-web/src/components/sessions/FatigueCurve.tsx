import {
	ChartContainer,
	ChartLegend,
	ChartLegendContent,
	ChartTooltip,
	ChartTooltipContent,
} from "@shiron/ui/components/ui/chart";
import { useMemo } from "react";
import {
	CartesianGrid,
	Line,
	LineChart,
	ReferenceLine,
	XAxis,
	YAxis,
} from "recharts";
import type { FatiguePointDto } from "@/api/model";
import { formatSongTimeMs } from "@/lib/sessions";

const chartConfig = {
	left: { label: "Left", color: "oklch(0.62 0.24 27)" },
	right: { label: "Right", color: "oklch(0.62 0.19 255)" },
} as const;

/**
 * Saber speed over the song, indexed to each hand's own baseline (first bucket =
 * 100%) so both lines share one axis. A downward slope reads as fatigue.
 */
export function FatigueCurve({ points }: { points: FatiguePointDto[] }) {
	const data = useMemo(() => {
		if (points.length === 0) return [];
		const baseLeft = Number(points[0].leftSpeed) || 1;
		const baseRight = Number(points[0].rightSpeed) || 1;
		return points.map((p) => ({
			time: Math.round(Number(p.songTimeMs) / 1000),
			left: (Number(p.leftSpeed) / baseLeft) * 100,
			right: (Number(p.rightSpeed) / baseRight) * 100,
		}));
	}, [points]);

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
					tickFormatter={(v: number) => `${Math.round(v)}%`}
					tickLine={false}
					axisLine={false}
					width={40}
					domain={["dataMin - 10", "dataMax + 10"]}
				/>
				<ReferenceLine y={100} strokeDasharray="4 4" stroke="var(--border)" />
				<ChartTooltip
					content={
						<ChartTooltipContent
							labelFormatter={(_, payload) => {
								const time = payload?.[0]?.payload?.time;
								return time != null ? formatSongTimeMs(time * 1000) : "";
							}}
							formatter={(value, name) => (
								<span className="flex w-full items-center justify-between gap-2">
									<span className="capitalize text-muted-foreground">
										{name}
									</span>
									<span className="font-mono tabular-nums">
										{Number(value).toFixed(0)}%
									</span>
								</span>
							)}
						/>
					}
				/>
				<Line
					dataKey="left"
					stroke="var(--color-left)"
					strokeWidth={2}
					dot={false}
					type="monotone"
				/>
				<Line
					dataKey="right"
					stroke="var(--color-right)"
					strokeWidth={2}
					dot={false}
					type="monotone"
				/>
				<ChartLegend content={<ChartLegendContent />} />
			</LineChart>
		</ChartContainer>
	);
}

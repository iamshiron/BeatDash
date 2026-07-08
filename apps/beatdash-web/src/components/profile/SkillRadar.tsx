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
	ChartLegend,
	ChartLegendContent,
	ChartTooltip,
	ChartTooltipContent,
} from "@shiron/ui/components/ui/chart";
import { useMemo } from "react";
import {
	PolarAngleAxis,
	PolarGrid,
	PolarRadiusAxis,
	Radar,
	RadarChart,
} from "recharts";
import type { SkillProfileDto } from "@/api/model";

const AXES = [
	{ key: "stream", label: "Stream" },
	{ key: "tech", label: "Tech" },
	{ key: "speed", label: "Speed" },
	{ key: "jumps", label: "Jumps" },
	{ key: "gimmick", label: "Gimmick" },
] as const;

const chartConfig = {
	skill: { label: "Skill", color: "oklch(0.62 0.19 255)" },
	exposure: { label: "Exposure", color: "oklch(0.72 0.17 152)" },
} satisfies ChartConfig;

/**
 * Bare skill radar chart, or nothing when there is no analyzed data. Card-less
 * so callers can place it in whatever chrome they need.
 */
export function SkillRadarChart({
	data,
}: {
	data: SkillProfileDto | null | undefined;
}) {
	const radarData = useMemo(() => {
		if (!data) return [];
		const byKey = new Map(data.characteristics.map((c) => [c.key, c]));
		return AXES.map((axis) => {
			const entry = byKey.get(axis.key);
			return {
				metric: axis.label,
				skill: entry ? Number(entry.skill) : 0,
				exposure: entry ? Number(entry.exposure) : 0,
			};
		});
	}, [data]);

	if (!data || Number(data.playsConsidered) === 0) return null;

	return (
		<ChartContainer
			config={chartConfig}
			className="mx-auto aspect-square w-full max-w-xs"
		>
			<RadarChart data={radarData} outerRadius="70%">
				<PolarGrid />
				<PolarAngleAxis dataKey="metric" />
				<PolarRadiusAxis domain={[0, 1]} tick={false} axisLine={false} />
				<Radar
					name="exposure"
					dataKey="exposure"
					stroke="var(--color-exposure)"
					fill="var(--color-exposure)"
					fillOpacity={0.1}
				/>
				<Radar
					name="skill"
					dataKey="skill"
					stroke="var(--color-skill)"
					fill="var(--color-skill)"
					fillOpacity={0.2}
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
				<ChartLegend content={<ChartLegendContent />} />
			</RadarChart>
		</ChartContainer>
	);
}

/**
 * The skill radar wrapped in a titled card, for the dashboard. Renders nothing
 * when there is no analyzed data.
 */
export function SkillRadar({
	data,
}: {
	data: SkillProfileDto | null | undefined;
}) {
	if (!data || Number(data.playsConsidered) === 0) return null;

	return (
		<Card>
			<CardHeader>
				<CardTitle>Skill profile</CardTitle>
				<CardDescription>
					Strengths across play styles, weighted by accuracy · based on{" "}
					{Number(data.playsConsidered)} analyzed plays.
				</CardDescription>
			</CardHeader>
			<CardContent>
				<SkillRadarChart data={data} />
			</CardContent>
		</Card>
	);
}

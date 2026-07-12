import {
	type ChartConfig,
	ChartContainer,
	ChartLegend,
	ChartLegendContent,
	ChartTooltip,
	ChartTooltipContent,
} from "@shiron/ui/components/ui/chart";
import {
	Select,
	SelectContent,
	SelectItem,
	SelectTrigger,
	SelectValue,
} from "@shiron/ui/components/ui/select";
import { useMemo, useState } from "react";
import { CartesianGrid, Line, LineChart, XAxis, YAxis } from "recharts";
import type { PlaySessionListItemDto } from "@/api/model";
import { useGetApiSessionsIdTimeline } from "@/api/sessions/sessions";
import { formatAccuracy, formatSongTimeMs } from "@/lib/sessions";

const COLOR_A = "oklch(0.62 0.19 255)";
const COLOR_B = "oklch(0.75 0.15 65)";

interface Point {
	songTimeMs: number | string;
}

/** Merge two time series (indexed by songTimeMs) into one dataset for overlaying. */
function merge<T extends Point>(
	a: T[],
	b: T[],
	value: (p: T) => number,
): { t: number; a: number | null; b: number | null }[] {
	const map = new Map<
		number,
		{ t: number; a: number | null; b: number | null }
	>();
	const put = (points: T[], key: "a" | "b") => {
		for (const p of points) {
			const t = Number(p.songTimeMs);
			const row = map.get(t) ?? { t, a: null, b: null };
			row[key] = value(p);
			map.set(t, row);
		}
	};
	put(a, "a");
	put(b, "b");
	return [...map.values()].sort((x, y) => x.t - y.t);
}

function OverlayChart({
	title,
	data,
	config,
	format,
	domain,
}: {
	title: string;
	data: { t: number; a: number | null; b: number | null }[];
	config: ChartConfig;
	format: (v: number) => string;
	domain?: [string | number, string | number];
}) {
	return (
		<div className="space-y-1">
			<span className="text-xs font-medium text-muted-foreground">{title}</span>
			<ChartContainer config={config} className="h-32 w-full">
				<LineChart
					data={data}
					margin={{ left: 4, right: 4, top: 4, bottom: 4 }}
				>
					<CartesianGrid strokeDasharray="3 3" vertical={false} />
					<XAxis
						dataKey="t"
						type="number"
						domain={["dataMin", "dataMax"]}
						tickFormatter={(v: number) => formatSongTimeMs(v)}
						tickLine={false}
						axisLine={false}
						tickMargin={8}
					/>
					<YAxis
						tickFormatter={(v: number) => format(v)}
						tickLine={false}
						axisLine={false}
						width={44}
						domain={domain}
					/>
					<ChartTooltip
						content={
							<ChartTooltipContent
								labelFormatter={(_, payload) => {
									const t = payload?.[0]?.payload?.t;
									return t != null ? formatSongTimeMs(Number(t)) : "";
								}}
								formatter={(value, name) => (
									<span className="flex w-full items-center justify-between gap-2">
										<span className="text-muted-foreground">{name}</span>
										<span className="font-mono tabular-nums">
											{format(Number(value))}
										</span>
									</span>
								)}
							/>
						}
					/>
					<Line
						dataKey="a"
						stroke={COLOR_A}
						strokeWidth={2}
						dot={false}
						type="monotone"
						connectNulls
					/>
					<Line
						dataKey="b"
						stroke={COLOR_B}
						strokeWidth={2}
						strokeDasharray="5 4"
						dot={false}
						type="monotone"
						connectNulls
					/>
					<ChartLegend content={<ChartLegendContent />} />
				</LineChart>
			</ChartContainer>
		</div>
	);
}

function label(attempts: PlaySessionListItemDto[], id: string): string {
	const idx = attempts.findIndex((s) => s.id === id);
	const s = attempts[idx];
	return `#${idx + 1} · ${formatAccuracy(Number(s?.results?.accuracy ?? 0))}`;
}

/**
 * Overlays two of the player's attempts on the same difficulty — cumulative
 * score and energy over song time — to see exactly where one run diverged from
 * the other. Attempt A is solid, attempt B dashed.
 */
export function AttemptCompare({
	attempts,
}: {
	attempts: PlaySessionListItemDto[];
}) {
	// Default to comparing the two most recent attempts.
	const [idA, setIdA] = useState(attempts[attempts.length - 2]?.id ?? "");
	const [idB, setIdB] = useState(attempts[attempts.length - 1]?.id ?? "");

	const timelineA = useGetApiSessionsIdTimeline(idA, {
		query: { enabled: !!idA },
	});
	const timelineB = useGetApiSessionsIdTimeline(idB, {
		query: { enabled: !!idB },
	});
	const tlA = timelineA.data?.status === 200 ? timelineA.data.data : null;
	const tlB = timelineB.data?.status === 200 ? timelineB.data.data : null;

	const labelA = label(attempts, idA);
	const labelB = label(attempts, idB);

	const scoreData = useMemo(
		() => merge(tlA?.score ?? [], tlB?.score ?? [], (p) => Number(p.score)),
		[tlA, tlB],
	);
	const energyData = useMemo(
		() =>
			merge(
				tlA?.energy ?? [],
				tlB?.energy ?? [],
				(p) => Number(p.energy) * 100,
			),
		[tlA, tlB],
	);

	const scoreConfig = {
		a: { label: labelA, color: COLOR_A },
		b: { label: labelB, color: COLOR_B },
	} satisfies ChartConfig;

	return (
		<div className="space-y-3 border-t border-border pt-4">
			<div className="flex flex-wrap items-center gap-2">
				<span className="text-xs font-medium text-muted-foreground">
					Compare attempts
				</span>
				<Select value={idA} onValueChange={setIdA}>
					<SelectTrigger size="sm" className="h-7 w-28 text-xs">
						<SelectValue />
					</SelectTrigger>
					<SelectContent>
						{attempts.map((s, i) => (
							<SelectItem key={s.id} value={s.id} className="text-xs">
								#{i + 1} · {formatAccuracy(Number(s.results?.accuracy ?? 0))}
							</SelectItem>
						))}
					</SelectContent>
				</Select>
				<span className="text-xs text-muted-foreground">vs</span>
				<Select value={idB} onValueChange={setIdB}>
					<SelectTrigger size="sm" className="h-7 w-28 text-xs">
						<SelectValue />
					</SelectTrigger>
					<SelectContent>
						{attempts.map((s, i) => (
							<SelectItem key={s.id} value={s.id} className="text-xs">
								#{i + 1} · {formatAccuracy(Number(s.results?.accuracy ?? 0))}
							</SelectItem>
						))}
					</SelectContent>
				</Select>
			</div>

			<OverlayChart
				title="Score"
				data={scoreData}
				config={scoreConfig}
				format={(v) => Math.round(v).toLocaleString()}
			/>
			<OverlayChart
				title="Energy"
				data={energyData}
				config={scoreConfig}
				format={(v) => `${Math.round(v)}%`}
				domain={[0, 100]}
			/>
		</div>
	);
}

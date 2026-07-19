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
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import {
	FireIcon,
	HeartPulseIcon,
	MapArrowSquareIcon,
	RunningIcon,
	ScaleIcon,
} from "@solar-icons/react/dynamic";
import { createFileRoute, redirect } from "@tanstack/react-router";
import {
	Area,
	Bar,
	CartesianGrid,
	ComposedChart,
	XAxis,
	YAxis,
} from "recharts";
import { useGetHealthOverview } from "@/api/health/health";
import type { HealthOverviewDto } from "@/api/model";
import { AnimatedNumber } from "@/components/common/AnimatedNumber";
import { AppShell } from "@/components/layout/AppShell";
import { StatTile } from "@/components/profile/StatTile";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export const Route = createFileRoute("/health")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
		// Keep the page (and deep links) hidden unless tracking is on.
		if (!context.auth.user?.healthTrackingEnabled) {
			throw redirect({ to: "/", replace: true });
		}
	},
	component: HealthPage,
});

function formatMinutes(min: number): string {
	const total = Math.round(min);
	const h = Math.floor(total / 60);
	const m = total % 60;
	return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

function HealthPage() {
	useDocumentTitle("Health");
	const query = useGetHealthOverview();
	const overview = query.data?.status === 200 ? query.data.data : null;

	return (
		<AppShell wide>
			<div className="mb-6">
				<h1 className="font-heading text-2xl font-bold tracking-tight">
					Health &amp; fitness
				</h1>
				<p className="mt-1 text-sm text-muted-foreground">
					Calories, active time and movement across your Beat Saber plays.
				</p>
			</div>

			{query.isLoading && (
				<div className="flex flex-col gap-4">
					<Skeleton className="h-20 rounded-xl" />
					<Skeleton className="h-56 rounded-xl" />
					<Skeleton className="h-40 rounded-xl" />
				</div>
			)}

			{!query.isLoading && overview && <HealthBody overview={overview} />}
		</AppShell>
	);
}

const trendConfig = {
	kcal: { label: "Calories", color: "oklch(0.64 0.21 25)" },
	minutes: { label: "Active min", color: "oklch(0.72 0.17 152)" },
} satisfies ChartConfig;

function formatWeek(weekStart: string): string {
	const [y, m, d] = weekStart.split("-").map(Number);
	return new Date(y, m - 1, d).toLocaleDateString(undefined, {
		month: "short",
		day: "numeric",
	});
}

function HealthBody({ overview }: { overview: HealthOverviewDto }) {
	const chartData = overview.trend.map((b) => ({
		week: formatWeek(b.weekStart),
		kcal: Math.round(Number(b.kcal)),
		minutes: Math.round(Number(b.activeMinutes)),
	}));

	const bmi = overview.bmi != null ? Number(overview.bmi) : null;
	const bmr =
		overview.bmrKcalPerDay != null ? Number(overview.bmrKcalPerDay) : null;
	const leanMass =
		overview.leanMassKg != null ? Number(overview.leanMassKg) : null;
	const restingHr =
		overview.restingHeartRate != null
			? Number(overview.restingHeartRate)
			: null;
	const recentHr =
		overview.recentAvgHeartRate != null
			? Number(overview.recentAvgHeartRate)
			: null;
	const hasBody =
		bmi != null || bmr != null || restingHr != null || recentHr != null;

	return (
		<div className="flex flex-col gap-6">
			<div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
				<StatTile
					icon={<FireIcon className="size-4" />}
					label="Career calories"
					value={
						<AnimatedNumber
							value={Number(overview.careerKcal)}
							format={(n) => Math.round(n).toLocaleString("en-US")}
						/>
					}
					sub="kcal burned"
				/>
				<StatTile
					icon={<RunningIcon className="size-4" />}
					label="Active time"
					value={
						<AnimatedNumber
							value={Number(overview.careerActiveMinutes)}
							format={formatMinutes}
						/>
					}
				/>
				<StatTile
					icon={<MapArrowSquareIcon className="size-4" />}
					label="Distance moved"
					value={
						<AnimatedNumber
							value={Number(overview.totalSaberTravelKm)}
							format={(n) => `${n.toFixed(1)} km`}
						/>
					}
					sub="saber travel"
				/>
				<StatTile
					icon={<FireIcon className="size-4" />}
					label="Avg / play"
					value={
						<AnimatedNumber
							value={Number(overview.avgKcalPerPlay)}
							format={(n) => `${Math.round(n)} kcal`}
						/>
					}
				/>
			</div>

			<Card>
				<CardHeader>
					<CardTitle>Weekly trend</CardTitle>
					<CardDescription>
						Calories and active minutes per week over the last 12 weeks.
					</CardDescription>
				</CardHeader>
				<CardContent>
					<ChartContainer config={trendConfig} className="h-56 w-full">
						<ComposedChart
							data={chartData}
							margin={{ left: 4, right: 4, top: 8, bottom: 4 }}
						>
							<defs>
								<linearGradient id="kcalFill" x1="0" y1="0" x2="0" y2="1">
									<stop
										offset="0%"
										stopColor="var(--color-kcal)"
										stopOpacity={0.3}
									/>
									<stop
										offset="100%"
										stopColor="var(--color-kcal)"
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
								yAxisId="kcal"
								tickLine={false}
								axisLine={false}
								width={40}
							/>
							<YAxis
								yAxisId="minutes"
								orientation="right"
								tickLine={false}
								axisLine={false}
								width={32}
								allowDecimals={false}
							/>
							<ChartTooltip content={<ChartTooltipContent />} />
							<Bar
								yAxisId="minutes"
								dataKey="minutes"
								fill="var(--color-minutes)"
								fillOpacity={0.25}
								radius={[2, 2, 0, 0]}
							/>
							<Area
								yAxisId="kcal"
								dataKey="kcal"
								stroke="var(--color-kcal)"
								strokeWidth={2}
								fill="url(#kcalFill)"
								type="monotone"
							/>
						</ComposedChart>
					</ChartContainer>
				</CardContent>
			</Card>

			{hasBody && (
				<Card>
					<CardHeader>
						<CardTitle>Body &amp; heart rate</CardTitle>
						<CardDescription>
							Derived from the metrics you entered in Settings.
						</CardDescription>
					</CardHeader>
					<CardContent>
						<div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
							{bmi != null && (
								<BodyStat
									icon={<ScaleIcon className="size-4" />}
									label="BMI"
									value={bmi.toFixed(1)}
								/>
							)}
							{bmr != null && (
								<BodyStat
									icon={<FireIcon className="size-4" />}
									label="BMR"
									value={`${Math.round(bmr)} kcal/day`}
								/>
							)}
							{leanMass != null && (
								<BodyStat
									icon={<ScaleIcon className="size-4" />}
									label="Lean mass"
									value={`${leanMass.toFixed(1)} kg`}
								/>
							)}
							{recentHr != null && (
								<BodyStat
									icon={<HeartPulseIcon className="size-4" />}
									label="Avg HR (7d)"
									value={`${Math.round(recentHr)} bpm`}
								/>
							)}
							{restingHr != null && (
								<BodyStat
									icon={<HeartPulseIcon className="size-4" />}
									label="Resting HR"
									value={`${restingHr} bpm`}
								/>
							)}
						</div>
					</CardContent>
				</Card>
			)}
		</div>
	);
}

function BodyStat({
	icon,
	label,
	value,
}: {
	icon: React.ReactNode;
	label: string;
	value: string;
}) {
	return (
		<div className="flex flex-col gap-1 rounded-lg border border-border bg-card/50 p-3">
			<span className="flex items-center gap-1.5 text-[11px] font-medium uppercase tracking-wide text-muted-foreground">
				{icon}
				{label}
			</span>
			<span className="font-heading text-lg font-bold tabular-nums">
				{value}
			</span>
		</div>
	);
}

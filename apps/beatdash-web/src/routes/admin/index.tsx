import { Button } from "@shiron/ui/components/ui/button";
import {
	Card,
	CardContent,
	CardDescription,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import {
	ChartSquareIcon,
	DevicesIcon,
	MusicNote4Icon,
	UserIcon,
} from "@solar-icons/react/dynamic";
import { useQuery } from "@tanstack/react-query";
import { createFileRoute } from "@tanstack/react-router";

import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export const Route = createFileRoute("/admin/")({
	component: AdminOverviewPage,
});

interface AdminOverview {
	users: number;
	devices: number;
	maps: number;
	plays: number;
	analysisPipeline: AnalysisPipeline;
}

interface AnalysisPipeline {
	difficulties: number;
	analyzed: number;
	analysisSucceeded: number;
	featuresExtracted: number;
	metricsScored: number;
}

const overviewQueryKey = ["/api/admin/metrics/overview"] as const;

async function getAdminOverview(): Promise<AdminOverview> {
	const response = await fetch(overviewQueryKey[0]);
	if (!response.ok) throw new Error("Unable to load the operational overview.");
	return response.json() as Promise<AdminOverview>;
}

function AdminOverviewPage() {
	useDocumentTitle("Admin overview");
	const overviewQuery = useQuery({
		queryKey: overviewQueryKey,
		queryFn: getAdminOverview,
	});

	return (
		<div className="space-y-6">
			<div>
				<h1 className="font-heading text-lg font-semibold tracking-tight">
					Overview
				</h1>
				<p className="mt-1 text-sm text-muted-foreground">
					Operational counts and map-analysis progress. Data excludes personal
					details.
				</p>
			</div>

			{overviewQuery.isError ? (
				<Card>
					<CardHeader>
						<CardTitle>Overview unavailable</CardTitle>
						<CardDescription>
							The operational summary could not be loaded.
						</CardDescription>
					</CardHeader>
					<CardContent>
						<Button
							onClick={() => void overviewQuery.refetch()}
							variant="outline"
						>
							Try again
						</Button>
					</CardContent>
				</Card>
			) : (
				<>
					<section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
						<OverviewCard
							icon={UserIcon}
							label="Users"
							value={overviewQuery.data?.users}
						/>
						<OverviewCard
							icon={DevicesIcon}
							label="Devices"
							value={overviewQuery.data?.devices}
						/>
						<OverviewCard
							icon={MusicNote4Icon}
							label="Maps"
							value={overviewQuery.data?.maps}
						/>
						<OverviewCard
							icon={ChartSquareIcon}
							label="Plays"
							value={overviewQuery.data?.plays}
						/>
					</section>

					<Card>
						<CardHeader>
							<CardTitle>Map analysis pipeline</CardTitle>
							<CardDescription>
								Completed stages across submitted map difficulties.
							</CardDescription>
						</CardHeader>
						<CardContent className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
							<PipelineCount
								label="Difficulties"
								value={overviewQuery.data?.analysisPipeline.difficulties}
							/>
							<PipelineCount
								label="Analyzed"
								value={overviewQuery.data?.analysisPipeline.analyzed}
							/>
							<PipelineCount
								label="Parsed successfully"
								value={overviewQuery.data?.analysisPipeline.analysisSucceeded}
							/>
							<PipelineCount
								label="Features extracted"
								value={overviewQuery.data?.analysisPipeline.featuresExtracted}
							/>
							<PipelineCount
								label="Metrics scored"
								value={overviewQuery.data?.analysisPipeline.metricsScored}
							/>
						</CardContent>
					</Card>
				</>
			)}
		</div>
	);
}

function OverviewCard({
	icon: Icon,
	label,
	value,
}: {
	icon: typeof UserIcon;
	label: string;
	value: number | undefined;
}) {
	return (
		<Card>
			<CardHeader className="flex-row items-center justify-between space-y-0 pb-2">
				<CardDescription>{label}</CardDescription>
				<Icon className="size-4 text-muted-foreground" />
			</CardHeader>
			<CardContent>
				{value === undefined ? (
					<Skeleton className="h-8 w-16" />
				) : (
					<div className="font-heading text-2xl font-semibold tracking-tight">
						{value.toLocaleString()}
					</div>
				)}
			</CardContent>
		</Card>
	);
}

function PipelineCount({
	label,
	value,
}: {
	label: string;
	value: number | undefined;
}) {
	return (
		<div className="rounded-lg border border-border p-4">
			<p className="text-sm text-muted-foreground">{label}</p>
			{value === undefined ? (
				<Skeleton className="mt-2 h-7 w-12" />
			) : (
				<p className="mt-1 font-heading text-xl font-semibold tracking-tight">
					{value.toLocaleString()}
				</p>
			)}
		</div>
	);
}

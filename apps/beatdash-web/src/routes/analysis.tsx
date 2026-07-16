import {
	Empty,
	EmptyDescription,
	EmptyHeader,
	EmptyMedia,
	EmptyTitle,
} from "@shiron/ui/components/ui/empty";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import { ChartSquareIcon } from "@solar-icons/react/dynamic";
import { createFileRoute, redirect } from "@tanstack/react-router";
import {
	useGetApiSessionsRecommendations,
	useGetApiSessionsSkillProgression,
	useGetApiSessionsWeakness,
} from "@/api/sessions/sessions";
import { CutDirectionMatrix } from "@/components/analysis/CutDirectionMatrix";
import { LifetimeNoteGrid } from "@/components/analysis/LifetimeNoteGrid";
import { PracticeRecommendations } from "@/components/analysis/PracticeRecommendations";
import { SkillProgression } from "@/components/analysis/SkillProgression";
import { AppShell } from "@/components/layout/AppShell";

export const Route = createFileRoute("/analysis")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	component: AnalysisPage,
});

function AnalysisPage() {
	const weaknessQuery = useGetApiSessionsWeakness();
	const progressionQuery = useGetApiSessionsSkillProgression();
	const recommendationsQuery = useGetApiSessionsRecommendations();

	const weakness =
		weaknessQuery.data?.status === 200 ? weaknessQuery.data.data : null;
	const progression =
		progressionQuery.data?.status === 200 ? progressionQuery.data.data : null;
	const recommendations =
		recommendationsQuery.data?.status === 200
			? recommendationsQuery.data.data
			: null;

	const loading = weaknessQuery.isLoading;
	const hasNotes = weakness != null && Number(weakness.notesConsidered) > 0;

	return (
		<AppShell wide>
			<div className="flex flex-col gap-6">
				<div>
					<h1 className="font-heading text-2xl font-bold">Analysis</h1>
					<p className="text-sm text-muted-foreground">
						Your career-wide performance, broken down for Beat Saber.
					</p>
				</div>

				{loading ? (
					<div className="flex flex-col gap-4">
						<Skeleton className="h-64 w-full" />
						<Skeleton className="h-56 w-full" />
					</div>
				) : !hasNotes ? (
					<Empty>
						<EmptyHeader>
							<EmptyMedia variant="icon">
								<ChartSquareIcon />
							</EmptyMedia>
							<EmptyTitle>No analysis yet</EmptyTitle>
							<EmptyDescription>
								Play a few maps with a paired device and your weaknesses,
								progression and practice suggestions will appear here.
							</EmptyDescription>
						</EmptyHeader>
					</Empty>
				) : (
					<div className="flex flex-col gap-4">
						<div className="grid gap-4 lg:grid-cols-2">
							<CutDirectionMatrix cells={weakness.cutDirectionMatrix} />
							<LifetimeNoteGrid cells={weakness.gridHeatmap} />
						</div>
						<SkillProgression data={progression} />
						<PracticeRecommendations recommendations={recommendations} />
					</div>
				)}
			</div>
		</AppShell>
	);
}

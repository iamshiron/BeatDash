import {
	ClockIcon,
	HeartIcon,
	LockIcon,
	MusicNotesSimpleIcon,
	PlaylistIcon,
	ShareNetworkIcon,
	TargetIcon,
	UserIcon,
} from "@phosphor-icons/react";
import { Avatar, AvatarFallback } from "@shiron/ui/components/ui/avatar";
import { Button } from "@shiron/ui/components/ui/button";
import {
	Empty,
	EmptyDescription,
	EmptyHeader,
	EmptyMedia,
	EmptyTitle,
} from "@shiron/ui/components/ui/empty";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import { cn } from "@shiron/ui/lib/utils";
import { createFileRoute } from "@tanstack/react-router";
import { toast } from "sonner";
import type { PublicProfileDto } from "@/api/model";
import { useGetPublicProfile } from "@/api/profiles/profiles";
import { ActivityHeatmap } from "@/components/dashboard/ActivityHeatmap";
import { formatPlayTime } from "@/components/dashboard/Dashboard";
import { AppShell } from "@/components/layout/AppShell";
import { MostPlayedRow } from "@/components/profile/MostPlayedRow";
import { SessionRow } from "@/components/profile/SessionRow";
import { SkillRadarChart } from "@/components/profile/SkillRadar";
import { formatAccuracy, formatScore, RANK_STYLES } from "@/lib/sessions";
import { getInitials } from "@/lib/user";

export const Route = createFileRoute("/u/$handle")({
	component: ProfilePage,
});

const RANK_ORDER = ["SSS", "SS", "S", "A", "B", "C", "D", "E"];

function ProfilePage() {
	const { handle } = Route.useParams();
	// The URL carries a leading "@"; the API keys on the bare handle.
	const cleanHandle = handle.replace(/^@/, "");
	const { data, isLoading } = useGetPublicProfile(cleanHandle);
	const profile = data?.status === 200 ? data.data : null;

	return (
		<AppShell wide>
			{isLoading && (
				<div className="space-y-3">
					<Skeleton className="h-20 rounded-xl" />
					<Skeleton className="h-16 rounded-xl" />
					<Skeleton className="h-40 rounded-xl" />
				</div>
			)}

			{!isLoading && !profile && (
				<Empty className="mt-10">
					<EmptyHeader>
						<EmptyMedia variant="icon">
							<UserIcon />
						</EmptyMedia>
						<EmptyTitle>Profile not found</EmptyTitle>
						<EmptyDescription>
							No player goes by @{cleanHandle}.
						</EmptyDescription>
					</EmptyHeader>
				</Empty>
			)}

			{!isLoading && profile && <ProfileBody profile={profile} />}
		</AppShell>
	);
}

function ProfileBody({ profile }: { profile: PublicProfileDto }) {
	const { stats, activity, skill, history } = profile;
	const hasSkill = Boolean(skill && Number(skill.playsConsidered) > 0);
	const hasActivity = Boolean(activity && activity.length > 0);
	const hasAnySection = Boolean(stats || hasActivity || hasSkill || history);

	function share() {
		navigator.clipboard
			.writeText(window.location.href)
			.then(() => toast.success("Profile link copied."))
			.catch(() => toast.error("Could not copy the link."));
	}

	return (
		<div className="flex flex-col gap-6">
			{/* Identity header */}
			<div className="flex items-center gap-4 rounded-xl border border-border bg-card p-4">
				<Avatar size="lg">
					<AvatarFallback>{getInitials(profile.displayName)}</AvatarFallback>
				</Avatar>
				<div className="min-w-0 flex-1">
					<h1 className="truncate font-heading text-2xl font-bold tracking-tight">
						{profile.displayName}
					</h1>
					<p className="truncate text-sm text-muted-foreground">
						@{profile.handle}
					</p>
				</div>
				<Button variant="outline" size="sm" onClick={share}>
					<ShareNetworkIcon className="size-4" />
					Share
				</Button>
			</div>

			{stats && (
				<div className="flex flex-wrap items-center gap-x-6 gap-y-3 rounded-xl border border-border bg-card px-4 py-3">
					<Metric
						icon={<MusicNotesSimpleIcon className="size-3" />}
						label="Plays"
						value={formatScore(Number(stats.totalPlays))}
					/>
					<Metric
						icon={<ClockIcon className="size-3" />}
						label="Play time"
						value={formatPlayTime(Number(stats.totalPlayTimeMs))}
					/>
					<Metric
						icon={<TargetIcon className="size-3" />}
						label="Avg accuracy"
						value={formatAccuracy(Number(stats.averageAccuracy))}
					/>
					<Metric
						label="Full combos"
						value={formatScore(Number(stats.fullCombos))}
					/>
					<Metric
						label="Unique maps"
						value={formatScore(Number(stats.uniqueMaps))}
					/>
					{stats.rankDistribution.length > 0 && (
						<div className="flex flex-wrap items-center gap-1.5 sm:ml-auto">
							{[...stats.rankDistribution]
								.sort(
									(a, b) =>
										RANK_ORDER.indexOf(a.rank) - RANK_ORDER.indexOf(b.rank),
								)
								.map((r) => (
									<span
										key={r.rank}
										className="inline-flex items-center gap-1 rounded-md bg-muted/40 px-1.5 py-0.5"
									>
										<span
											className={cn(
												"font-heading text-sm font-bold",
												RANK_STYLES[r.rank] ?? "text-muted-foreground",
											)}
										>
											{r.rank}
										</span>
										<span className="font-mono text-[11px] tabular-nums text-muted-foreground">
											{formatScore(Number(r.count))}
										</span>
									</span>
								))}
						</div>
					)}
				</div>
			)}

			{(hasSkill || hasActivity) && (
				<div className="grid gap-6 lg:grid-cols-2">
					{hasSkill && skill && (
						<Section
							title="Skill profile"
							subtitle={`${Number(skill.playsConsidered)} plays`}
						>
							<SkillRadarChart data={skill} />
						</Section>
					)}
					{hasActivity && activity && (
						<Section title="Activity">
							<ActivityHeatmap activity={activity} />
						</Section>
					)}
				</div>
			)}

			{history && (
				<div className="grid gap-6 sm:grid-cols-2">
					<Section title="Recent plays">
						<div className="flex flex-col gap-1.5">
							{history.recentSessions.map((s) => (
								<SessionRow key={s.id} session={s} interactive={false} />
							))}
						</div>
					</Section>
					<Section title="Best accuracy">
						<div className="flex flex-col gap-1.5">
							{history.topScores.map((s) => (
								<SessionRow key={s.id} session={s} interactive={false} />
							))}
						</div>
					</Section>
				</div>
			)}

			{stats && stats.mostPlayedMaps.length > 0 && (
				<Section title="Most played">
					<div className="grid gap-1.5 sm:grid-cols-2">
						{stats.mostPlayedMaps.map((m) => (
							<MostPlayedRow key={m.beatmapId} map={m} interactive={false} />
						))}
					</div>
				</Section>
			)}

			{/* Upcoming: curated playlists and liked maps. */}
			{hasAnySection && (
				<div className="flex flex-col gap-2 sm:flex-row">
					<PlaceholderChip
						icon={<PlaylistIcon className="size-4" />}
						label="Playlists"
					/>
					<PlaceholderChip
						icon={<HeartIcon className="size-4" />}
						label="Liked maps"
					/>
				</div>
			)}

			{!hasAnySection && (
				<Empty className="mt-4">
					<EmptyHeader>
						<EmptyMedia variant="icon">
							<LockIcon />
						</EmptyMedia>
						<EmptyTitle>This profile is private</EmptyTitle>
						<EmptyDescription>
							{profile.displayName} hasn't shared any stats yet.
						</EmptyDescription>
					</EmptyHeader>
				</Empty>
			)}
		</div>
	);
}

function Metric({
	icon,
	label,
	value,
}: {
	icon?: React.ReactNode;
	label: string;
	value: string;
}) {
	return (
		<div className="flex flex-col">
			<span className="font-heading text-xl font-bold leading-tight tabular-nums">
				{value}
			</span>
			<span className="flex items-center gap-1 text-[11px] text-muted-foreground">
				{icon}
				{label}
			</span>
		</div>
	);
}

function Section({
	title,
	subtitle,
	children,
}: {
	title: string;
	subtitle?: string;
	children: React.ReactNode;
}) {
	return (
		<section className="flex flex-col gap-2.5">
			<div className="flex items-baseline justify-between gap-2">
				<h2 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
					{title}
				</h2>
				{subtitle && (
					<span className="text-[11px] text-muted-foreground/70">
						{subtitle}
					</span>
				)}
			</div>
			{children}
		</section>
	);
}

function PlaceholderChip({
	icon,
	label,
}: {
	icon: React.ReactNode;
	label: string;
}) {
	return (
		<div className="flex flex-1 items-center gap-2 rounded-lg border border-dashed border-border px-3 py-2">
			<span className="text-muted-foreground/60">{icon}</span>
			<span className="text-xs font-medium">{label}</span>
			<span className="ml-auto text-[10px] font-medium uppercase tracking-wide text-muted-foreground/50">
				Soon
			</span>
		</div>
	);
}

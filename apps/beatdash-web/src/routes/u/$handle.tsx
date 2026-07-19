import {
	Avatar,
	AvatarFallback,
	AvatarImage,
} from "@shiron/ui/components/ui/avatar";
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
import {
	CameraIcon,
	ClockCircleIcon,
	LockIcon,
	MusicNotesIcon,
	PenIcon,
	PlaylistIcon,
	ShareIcon,
	TargetIcon,
	UserIcon,
} from "@solar-icons/react/dynamic";
import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link } from "@tanstack/react-router";
import { useRef, useState } from "react";
import { toast } from "sonner";
import { useUploadAvatar, useUploadBanner } from "@/api/auth/auth";
import { getGetApiMapsMapIdCoverUrl } from "@/api/maps/maps";
import type { PublicLikedMapDto, PublicPlaylistDto } from "@/api/model";
import type { PublicProfileDto } from "@/api/model";
import {
	getGetPublicProfileQueryKey,
	useGetPublicProfile,
} from "@/api/profiles/profiles";
import { AnimatedNumber } from "@/components/common/AnimatedNumber";
import { ActivityHeatmap } from "@/components/dashboard/ActivityHeatmap";
import { formatPlayTime } from "@/components/dashboard/Dashboard";
import { AppShell } from "@/components/layout/AppShell";
import { MostPlayedRow } from "@/components/profile/MostPlayedRow";
import { SessionRow } from "@/components/profile/SessionRow";
import { SkillRadarChart } from "@/components/profile/SkillRadar";
import { getGetMeQueryKey, useAuth } from "@/contexts/auth";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";
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
	useDocumentTitle(
		profile ? `${profile.displayName} (@${profile.handle})` : `@${cleanHandle}`,
	);

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
	const { user } = useAuth();
	// Editing affordances only surface when you're looking at your own profile.
	const isOwnProfile = Boolean(user?.handle && user.handle === profile.handle);
	const { stats, activity, skill, history } = profile;
	const hasSkill = Boolean(skill && Number(skill.playsConsidered) > 0);
	const hasActivity = Boolean(activity && activity.length > 0);
	const hasPlaylists = Boolean(profile.playlists && profile.playlists.length > 0);
	const hasLiked = Boolean(profile.likedMaps && profile.likedMaps.length > 0);
	const hasAnySection = Boolean(
		stats || hasActivity || hasSkill || history || hasPlaylists || hasLiked,
	);
	// Personalise the banner with the player's most-played cover when their stats
	// are public; otherwise fall back to a handle-seeded wash.
	const bannerMapId = stats?.mostPlayedMaps?.[0]?.beatmapId ?? null;

	const queryClient = useQueryClient();
	const avatarInputRef = useRef<HTMLInputElement>(null);
	const bannerInputRef = useRef<HTMLInputElement>(null);

	const refreshProfile = () =>
		Promise.all([
			queryClient.invalidateQueries({
				queryKey: getGetPublicProfileQueryKey(profile.handle),
			}),
			queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() }),
		]);

	const avatarUpload = useUploadAvatar({
		mutation: {
			onSuccess: async (res) => {
				if (res.status !== 200) return toast.error("Couldn't update your avatar.");
				await refreshProfile();
				toast.success("Avatar updated.");
			},
			onError: () => toast.error("Couldn't update your avatar."),
		},
	});
	const bannerUpload = useUploadBanner({
		mutation: {
			onSuccess: async (res) => {
				if (res.status !== 200) return toast.error("Couldn't update your banner.");
				await refreshProfile();
				toast.success("Banner updated.");
			},
			onError: () => toast.error("Couldn't update your banner."),
		},
	});

	function pickImage(
		event: React.ChangeEvent<HTMLInputElement>,
		kind: "avatar" | "banner",
	) {
		const file = event.target.files?.[0];
		// Reset so re-picking the same file still fires onChange.
		event.target.value = "";
		if (!file) return;
		if (file.size > 5 * 1024 * 1024) {
			toast.error("Image is too large (max 5 MB).");
			return;
		}
		if (kind === "avatar") avatarUpload.mutate({ data: { file } });
		else bannerUpload.mutate({ data: { file } });
	}

	function share() {
		navigator.clipboard
			.writeText(window.location.href)
			.then(() => toast.success("Profile link copied."))
			.catch(() => toast.error("Could not copy the link."));
	}

	return (
		<div className="flex flex-col gap-6">
			{isOwnProfile && (
				<>
					<input
						ref={avatarInputRef}
						type="file"
						accept="image/png,image/jpeg,image/webp,image/gif"
						className="hidden"
						onChange={(e) => pickImage(e, "avatar")}
					/>
					<input
						ref={bannerInputRef}
						type="file"
						accept="image/png,image/jpeg,image/webp,image/gif"
						className="hidden"
						onChange={(e) => pickImage(e, "banner")}
					/>
				</>
			)}
			{/* Identity header — banner with an overlapping avatar, Discord-style */}
			<div className="mx-auto w-full max-w-lg overflow-hidden rounded-xl border border-border bg-card">
				<ProfileBanner
					seed={profile.handle}
					coverMapId={bannerMapId}
					bannerUrl={profile.bannerUrl}
					editable={isOwnProfile}
					onEdit={() => bannerInputRef.current?.click()}
				/>
				<div className="px-4 pb-4">
					<div className="flex flex-wrap items-end gap-x-4 gap-y-2">
						<div className="group/avatar relative -mt-12 size-24 shrink-0">
							<Avatar className="size-full ring-4 ring-card">
								{profile.avatarUrl && (
									<AvatarImage
										src={profile.avatarUrl}
										alt={profile.displayName}
									/>
								)}
								<AvatarFallback className="text-3xl font-semibold">
									{getInitials(profile.displayName)}
								</AvatarFallback>
							</Avatar>
							{isOwnProfile && (
								<button
									type="button"
									aria-label="Change profile picture"
									onClick={() => avatarInputRef.current?.click()}
									className="absolute inset-0 flex items-center justify-center rounded-full bg-black/55 text-white opacity-0 outline-none transition-opacity group-hover/avatar:opacity-100 focus-visible:opacity-100"
								>
									<CameraIcon className="size-6" weight="Bold" />
								</button>
							)}
						</div>
						<div className="min-w-0 flex-1 pt-1">
							<div className="flex items-center gap-1.5">
								<h1 className="truncate font-heading text-2xl font-bold tracking-tight">
									{profile.displayName}
								</h1>
								{isOwnProfile && <EditPencil label="Change display name" />}
							</div>
							<div className="flex items-center gap-1.5">
								<p className="truncate text-sm text-muted-foreground">
									@{profile.handle}
								</p>
								{isOwnProfile && <EditPencil label="Change handle" />}
							</div>
						</div>
						<Button
							variant="outline"
							size="sm"
							onClick={share}
							className="mb-1 ml-auto"
						>
							<ShareIcon className="size-4" />
							Share
						</Button>
					</div>

					{stats && (
						<div className="mt-4 flex flex-wrap items-center gap-x-6 gap-y-3 border-t border-border pt-4">
							<Metric
								icon={<MusicNotesIcon className="size-3" />}
								label="Plays"
								value={
									<AnimatedNumber
										value={Number(stats.totalPlays)}
										format={(n) => formatScore(Math.round(n))}
									/>
								}
							/>
							<Metric
								icon={<ClockCircleIcon className="size-3" />}
								label="Play time"
								value={
									<AnimatedNumber
										value={Number(stats.totalPlayTimeMs)}
										format={formatPlayTime}
									/>
								}
							/>
							<Metric
								icon={<TargetIcon className="size-3" />}
								label="Avg accuracy"
								value={
									<AnimatedNumber
										value={Number(stats.averageAccuracy)}
										format={formatAccuracy}
									/>
								}
							/>
							<Metric
								label="Full combos"
								value={
									<AnimatedNumber
										value={Number(stats.fullCombos)}
										format={(n) => formatScore(Math.round(n))}
									/>
								}
							/>
							<Metric
								label="Unique maps"
								value={
									<AnimatedNumber
										value={Number(stats.uniqueMaps)}
										format={(n) => formatScore(Math.round(n))}
									/>
								}
							/>
							{stats.rankDistribution.length > 0 && (
								<div className="flex w-full flex-wrap items-center gap-1.5">
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
				</div>
			</div>

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

			{hasPlaylists && profile.playlists && (
				<Section title="Playlists">
					<div className="grid gap-2 sm:grid-cols-2">
						{profile.playlists.map((playlist) => (
							<PlaylistCard key={playlist.id} playlist={playlist} />
						))}
					</div>
				</Section>
			)}

			{hasLiked && profile.likedMaps && (
				<Section title="Liked maps">
					<div className="grid gap-1.5 sm:grid-cols-2">
						{profile.likedMaps.map((map) => (
							<LikedMapRow key={map.beatmapId} map={map} />
						))}
					</div>
				</Section>
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

/** A subtle inline pencil linking to settings to edit an identity field (own profile only). */
function EditPencil({ label }: { label: string }) {
	return (
		<Link
			to="/settings"
			aria-label={label}
			className="flex size-6 shrink-0 items-center justify-center rounded-md text-muted-foreground/60 opacity-70 transition hover:bg-foreground/10 hover:text-foreground hover:opacity-100 focus-visible:opacity-100"
		>
			<PenIcon className="size-3.5" />
		</Link>
	);
}

/** A display-only playlist card for a public profile. */
function PlaylistCard({ playlist }: { playlist: PublicPlaylistDto }) {
	const [failed, setFailed] = useState(false);
	const cover = playlist.coverMapIds[0];
	return (
		<div className="flex min-w-0 items-center gap-3 rounded-lg border border-border bg-card p-2">
			<div className="flex size-10 shrink-0 items-center justify-center overflow-hidden rounded-md bg-gradient-to-br from-primary/40 to-[oklch(0.62_0.19_255)]/40">
				{cover && !failed ? (
					<img
						src={getGetApiMapsMapIdCoverUrl(cover)}
						alt=""
						loading="lazy"
						onError={() => setFailed(true)}
						className="size-full object-cover"
					/>
				) : (
					<PlaylistIcon className="size-4 text-muted-foreground/50" />
				)}
			</div>
			<div className="min-w-0 flex-1">
				<span className="block truncate font-heading text-sm font-semibold">
					{playlist.name}
				</span>
				<span className="block text-[10px] text-muted-foreground/60">
					{Number(playlist.mapCount)}{" "}
					{Number(playlist.mapCount) === 1 ? "map" : "maps"}
				</span>
			</div>
		</div>
	);
}

/** A display-only liked-map row for a public profile. */
function LikedMapRow({ map }: { map: PublicLikedMapDto }) {
	const [failed, setFailed] = useState(false);
	return (
		<div className="flex min-w-0 items-center gap-3 rounded-lg border border-border bg-card p-2">
			<div className="size-10 shrink-0 overflow-hidden rounded-md bg-gradient-to-br from-primary/40 to-[oklch(0.62_0.19_255)]/40">
				{!failed ? (
					<img
						src={getGetApiMapsMapIdCoverUrl(map.beatmapId)}
						alt=""
						loading="lazy"
						onError={() => setFailed(true)}
						className="size-full object-cover"
					/>
				) : (
					<div className="flex size-full items-center justify-center">
						<MusicNotesIcon className="size-4 text-muted-foreground/40" />
					</div>
				)}
			</div>
			<div className="min-w-0 flex-1">
				<span className="block truncate font-heading text-sm font-semibold">
					{map.songName}
				</span>
				<span className="block truncate text-[10px] text-muted-foreground/60">
					{map.songAuthor} · {map.mapper}
				</span>
			</div>
		</div>
	);
}

/**
 * A muted two-tone wash derived from the handle, so every profile without a
 * cover-art banner still gets a stable, distinct colour. Kept low-saturation and
 * same-family (hues ~35° apart) to read as a refined backdrop, not a loud gradient.
 */
function bannerGradient(seed: string): string {
	let hue = 0;
	for (let i = 0; i < seed.length; i++) {
		hue = (hue * 31 + seed.charCodeAt(i)) % 360;
	}
	const hue2 = (hue + 35) % 360;
	return `linear-gradient(120deg, oklch(0.42 0.08 ${hue}), oklch(0.3 0.06 ${hue2}))`;
}

/**
 * Profile banner. Prefers the user's uploaded banner; otherwise falls back to a
 * blurred most-played cover, then a seeded wash.
 */
function ProfileBanner({
	seed,
	coverMapId,
	bannerUrl,
	editable = false,
	onEdit,
}: {
	seed: string;
	coverMapId: string | null;
	/** The user's uploaded banner URL, when set. */
	bannerUrl?: string | null;
	/** Show a hover "change banner" affordance (own profile only). */
	editable?: boolean;
	onEdit?: () => void;
}) {
	const [coverFailed, setCoverFailed] = useState(false);
	const showCover = !bannerUrl && coverMapId !== null && !coverFailed;

	return (
		<div className="group relative aspect-[2.83/1] w-full overflow-hidden">
			{bannerUrl ? (
				<img src={bannerUrl} alt="" aria-hidden className="size-full object-cover" />
			) : showCover ? (
				<img
					src={getGetApiMapsMapIdCoverUrl(coverMapId)}
					alt=""
					aria-hidden
					onError={() => setCoverFailed(true)}
					className="size-full scale-125 object-cover blur-2xl brightness-[0.55] saturate-[0.65]"
				/>
			) : (
				<div
					className="size-full"
					style={{ backgroundImage: bannerGradient(seed) }}
				/>
			)}
			{/* Blend the bottom into the card so the overlapping avatar reads cleanly.
			    An uploaded banner gets a lighter scrim so it stays visible. */}
			<div
				className={cn(
					"absolute inset-0",
					bannerUrl
						? "bg-gradient-to-t from-card/90 to-transparent"
						: "bg-gradient-to-t from-card via-card/60 to-card/30",
				)}
			/>
			{editable && (
				<button
					type="button"
					aria-label="Change banner"
					onClick={onEdit}
					className="absolute top-2 right-2 flex items-center gap-1.5 rounded-md bg-background/70 px-2 py-1 text-xs font-medium text-foreground opacity-0 outline-none backdrop-blur-sm transition hover:bg-background/90 group-hover:opacity-100 focus-visible:opacity-100"
				>
					<CameraIcon className="size-3.5" weight="Bold" />
					Edit banner
				</button>
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
	value: React.ReactNode;
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
		<section className="flex min-w-0 flex-col gap-2.5">
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

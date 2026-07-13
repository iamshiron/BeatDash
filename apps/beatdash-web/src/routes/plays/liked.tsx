import { Button } from "@shiron/ui/components/ui/button";
import {
	Empty,
	EmptyDescription,
	EmptyHeader,
	EmptyMedia,
	EmptyTitle,
} from "@shiron/ui/components/ui/empty";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import { AltArrowLeft, AltArrowRight, Heart } from "@solar-icons/react";
import { keepPreviousData } from "@tanstack/react-query";
import {
	createFileRoute,
	Link,
	redirect,
	useNavigate,
} from "@tanstack/react-router";
import { useGetApiMaps } from "@/api/maps/maps";
import { AppShell } from "@/components/layout/AppShell";
import { MapCard } from "@/components/maps/MapCard";

const PAGE_SIZE = 24;

type LikedSearch = { p?: number };

export const Route = createFileRoute("/plays/liked")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	validateSearch: (search: Record<string, unknown>): LikedSearch => ({
		p: Math.max(1, Number(search.p) || 1),
	}),
	component: LikedMapsPage,
});

const SKELETON_KEYS = Array.from(
	{ length: 6 },
	(_, i) => `liked-skeleton-${i}`,
);

function LikedMapsPage() {
	const { p = 1 } = Route.useSearch();
	const navigate = useNavigate();

	const { data, isLoading } = useGetApiMaps(
		{ Page: p, PageSize: PAGE_SIZE, Liked: true },
		{ query: { placeholderData: keepPreviousData } },
	);

	const result = data?.status === 200 ? data.data : undefined;
	const maps = result?.items ?? [];
	const totalCount = result ? Number(result.totalCount) : 0;
	const totalPages = result ? Number(result.totalPages) : 0;

	const goToPage = (page: number) =>
		navigate({ to: "/plays/liked", search: { p: page } });

	return (
		<AppShell wide>
			<div className="flex flex-wrap items-end justify-between gap-3">
				<div>
					<h1 className="font-heading text-xl font-bold tracking-tight">
						Liked maps
					</h1>
					<p className="mt-0.5 text-xs text-muted-foreground">
						{totalCount} {totalCount === 1 ? "map" : "maps"}
					</p>
				</div>
				<div className="flex items-center gap-1 rounded-lg border border-border bg-muted/30 p-0.5">
					<Button
						variant="ghost"
						size="sm"
						className="h-7 px-2.5 text-xs"
						asChild
					>
						<Link to="/plays">Plays</Link>
					</Button>
					<Button variant="secondary" size="sm" className="h-7 px-2.5 text-xs">
						Liked maps
					</Button>
				</div>
			</div>

			{isLoading && (
				<div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
					{SKELETON_KEYS.map((key) => (
						<Skeleton key={key} className="h-28 rounded-xl" />
					))}
				</div>
			)}

			{!isLoading && maps.length === 0 && (
				<Empty className="mt-10">
					<EmptyHeader>
						<EmptyMedia variant="icon">
							<Heart />
						</EmptyMedia>
						<EmptyTitle>No liked maps yet</EmptyTitle>
						<EmptyDescription>
							Tap the heart on any map to save it here.
						</EmptyDescription>
					</EmptyHeader>
				</Empty>
			)}

			{!isLoading && maps.length > 0 && (
				<div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
					{maps.map((map) => (
						<MapCard key={map.id} map={map} />
					))}
				</div>
			)}

			{!isLoading && totalPages > 1 && (
				<div className="mt-6 flex items-center justify-between">
					<p className="font-mono text-xs tabular-nums text-muted-foreground">
						{p} / {totalPages}
					</p>
					<div className="flex items-center gap-1.5">
						<Button
							variant="outline"
							size="icon"
							className="size-8"
							disabled={p <= 1}
							onClick={() => goToPage(p - 1)}
						>
							<AltArrowLeft className="size-4" />
						</Button>
						<Button
							variant="outline"
							size="icon"
							className="size-8"
							disabled={p >= totalPages}
							onClick={() => goToPage(p + 1)}
						>
							<AltArrowRight className="size-4" />
						</Button>
					</div>
				</div>
			)}
		</AppShell>
	);
}

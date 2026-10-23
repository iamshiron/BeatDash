import { Button } from "@shiron/ui/components/ui/button";
import {
	Empty,
	EmptyDescription,
	EmptyHeader,
	EmptyMedia,
	EmptyTitle,
} from "@shiron/ui/components/ui/empty";
import { Input } from "@shiron/ui/components/ui/input";
import {
	Pagination,
	PaginationContent,
	PaginationEllipsis,
	PaginationItem,
	PaginationLink,
	PaginationNext,
	PaginationPrevious,
} from "@shiron/ui/components/ui/pagination";
import {
	Select,
	SelectContent,
	SelectItem,
	SelectTrigger,
	SelectValue,
} from "@shiron/ui/components/ui/select";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import {
	HeartIcon,
	MagnifierIcon,
	MusicNotesIcon,
	SortVerticalIcon,
} from "@solar-icons/react/dynamic";
import { useDebouncedCallback } from "@tanstack/react-pacer";
import { keepPreviousData } from "@tanstack/react-query";
import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";
import { memo, useEffect, useState } from "react";
import { useGetApiMaps } from "@/api/maps/maps";
import type { MapListItemDto } from "@/api/model";
import { ErrorState } from "@/components/common/ErrorState";
import { AppShell } from "@/components/layout/AppShell";
import { MapCard } from "@/components/maps/MapCard";
import { MapFilters } from "@/components/maps/MapFilters";
import {
	DEFAULT_DIRECTION,
	DEFAULT_SORT,
	MAP_SORT_OPTIONS,
	type MapsSearch,
	mapsSearchToApi,
	normalizeMapsSearch,
	type SortDirection,
} from "@/lib/maps";
import { type MapProcessingEvent, useRealtimeEvent } from "@/realtime";

const SEARCH_DEBOUNCE_MS = 300;

const PLAYED_FILTERS: { value: MapsSearch["played"]; label: string }[] = [
	{ value: undefined, label: "All" },
	{ value: "played", label: "Played" },
	{ value: "unplayed", label: "Unplayed" },
];

/** A compact page list: first, last, and a window around the current page, gaps → ellipsis. */
function pageItems(
	current: number,
	total: number,
): { key: string; page: number | null }[] {
	const wanted = new Set([1, total, current - 1, current, current + 1]);
	const pages = [...wanted]
		.filter((p) => p >= 1 && p <= total)
		.sort((a, b) => a - b);

	const items: { key: string; page: number | null }[] = [];
	let prev = 0;
	for (const p of pages) {
		if (p - prev > 1) items.push({ key: `gap-${prev}-${p}`, page: null });
		items.push({ key: `p-${p}`, page: p });
		prev = p;
	}
	return items;
}

import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export const Route = createFileRoute("/maps/")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	validateSearch: normalizeMapsSearch,
	component: MapsPage,
});

// Isolated + memoized: typing in the search box never re-renders the card grid.
const MapGrid = memo(function MapGrid({ maps }: { maps: MapListItemDto[] }) {
	return (
		<div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
			{maps.map((map) => (
				<MapCard key={map.id} map={map} />
			))}
		</div>
	);
});

function MapsPage() {
	useDocumentTitle("Maps");
	const search = Route.useSearch();
	const {
		q = "",
		p = 1,
		played,
		liked,
		sort = DEFAULT_SORT,
		direction = DEFAULT_DIRECTION,
	} = search;
	const navigate = useNavigate();

	// Live BeatSaver import/processing progress, pushed over SignalR.
	const [processing, setProcessing] = useState<MapProcessingEvent | null>(null);
	useRealtimeEvent("receiveMapProcessing", setProcessing);

	// The box tracks keystrokes immediately; the URL (and thus the query) is only
	// updated once typing settles, so the grid isn't refetched on every character.
	const [input, setInput] = useState(q);
	useEffect(() => setInput(q), [q]);

	const commitSearch = useDebouncedCallback(
		(value: string) => {
			navigate({ to: "/maps", search: { ...search, q: value, p: 1 } });
		},
		{ wait: SEARCH_DEBOUNCE_MS },
	);

	const { data, isLoading, isError, refetch } = useGetApiMaps(
		mapsSearchToApi({ ...search, p, q: q.trim() || undefined }),
		{ query: { placeholderData: keepPreviousData } },
	);

	const result = data?.status === 200 ? data.data : undefined;
	const maps = result?.items ?? [];
	const totalPages = result ? Number(result.totalPages) : 0;
	const hasError = isError || (data != null && data.status >= 500);

	const goToPage = (page: number) =>
		navigate({ to: "/maps", search: { ...search, p: page } });

	const changeFilters = (updates: Partial<MapsSearch>) =>
		navigate({ to: "/maps", search: { ...search, ...updates, p: 1 } });

	const resetFilters = () =>
		navigate({
			to: "/maps",
			search: { q, p: 1, played, liked, sort, direction },
		});

	const changeSort = (value: string) => {
		const nextSort = MAP_SORT_OPTIONS.find(
			(option) => option.value === value,
		)?.value;
		if (!nextSort) return;
		navigate({ to: "/maps", search: { ...search, sort: nextSort, p: 1 } });
	};

	const changeDirection = (value: string) => {
		if (value !== "asc" && value !== "desc") return;
		const nextDirection: SortDirection = value;
		navigate({
			to: "/maps",
			search: { ...search, direction: nextDirection, p: 1 },
		});
	};

	return (
		<AppShell wide>
			<div className="flex flex-wrap items-center justify-between gap-3">
				<h1 className="font-heading text-lg font-semibold tracking-tight">
					Maps
				</h1>
				<div className="flex w-full flex-wrap items-center gap-2 sm:w-auto sm:justify-end">
					<MapFilters
						search={search}
						onChange={changeFilters}
						onReset={resetFilters}
					/>
					<Select value={sort} onValueChange={changeSort}>
						<SelectTrigger className="h-9 w-40 gap-1.5" aria-label="Sort maps">
							<SortVerticalIcon className="size-4 text-muted-foreground" />
							<SelectValue />
						</SelectTrigger>
						<SelectContent position="popper" align="end">
							{MAP_SORT_OPTIONS.map((option) => (
								<SelectItem key={option.value} value={option.value}>
									{option.label}
								</SelectItem>
							))}
						</SelectContent>
					</Select>
					<Select value={direction} onValueChange={changeDirection}>
						<SelectTrigger
							className="h-9 w-32 shrink-0"
							aria-label="Sort direction"
						>
							<SelectValue />
						</SelectTrigger>
						<SelectContent position="popper" align="end">
							<SelectItem value="desc">Descending</SelectItem>
							<SelectItem value="asc">Ascending</SelectItem>
						</SelectContent>
					</Select>
					<div className="flex items-center gap-1 rounded-lg border border-border bg-muted/30 p-0.5">
						{PLAYED_FILTERS.map((filter) => {
							const isActive = played === filter.value;
							return (
								<Button
									key={filter.label}
									type="button"
									variant={isActive ? "secondary" : "ghost"}
									size="sm"
									className="h-7 px-2.5 text-xs"
									onClick={() => changeFilters({ played: filter.value })}
								>
									{filter.label}
								</Button>
							);
						})}
					</div>
					<Button
						type="button"
						variant={liked ? "secondary" : "ghost"}
						size="sm"
						className={`h-8 gap-1.5 px-2.5 text-xs ${
							liked ? "text-rose-500" : ""
						}`}
						onClick={() => changeFilters({ liked: liked ? undefined : true })}
					>
						<HeartIcon className="size-4" weight={liked ? "Bold" : "Linear"} />
						Liked
					</Button>
					<div className="relative w-full sm:max-w-xs">
						<MagnifierIcon className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
						<Input
							value={input}
							onChange={(e) => {
								setInput(e.target.value);
								commitSearch(e.target.value);
							}}
							placeholder="Search maps…"
							className="h-9 pl-8"
						/>
					</div>
				</div>
			</div>

			{processing && processing.pending > 0 && (
				<div className="mt-3 flex items-center gap-2.5 rounded-lg border border-border bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
					<span className="relative flex size-2">
						<span className="absolute inline-flex size-full animate-ping rounded-full bg-primary/60" />
						<span className="relative inline-flex size-2 rounded-full bg-primary" />
					</span>
					<span>
						Processing…{" "}
						<span className="font-mono tabular-nums text-foreground">
							({processing.processed}/{processing.total})
						</span>
					</span>
					<div className="ml-auto h-1 w-28 overflow-hidden rounded-full bg-muted">
						<div
							className="h-full rounded-full bg-primary transition-[width]"
							style={{
								width: `${
									processing.total > 0
										? (processing.processed / processing.total) * 100
										: 0
								}%`,
							}}
						/>
					</div>
				</div>
			)}

			{isLoading && (
				<div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
					{["skel-a", "skel-b", "skel-c", "skel-d", "skel-e", "skel-f"].map(
						(key) => (
							<Skeleton key={key} className="h-28 rounded-xl" />
						),
					)}
				</div>
			)}

			{!isLoading && hasError && (
				<ErrorState title="Couldn't load maps" onRetry={() => refetch()} />
			)}

			{!isLoading && !hasError && maps.length === 0 && (
				<Empty className="mt-10">
					<EmptyHeader>
						<EmptyMedia variant="icon">
							<MusicNotesIcon />
						</EmptyMedia>
						<EmptyTitle>{q ? "No maps found" : "No maps yet"}</EmptyTitle>
						<EmptyDescription>
							{q
								? "Try a different search term."
								: "Play a map on your headset to see it here."}
						</EmptyDescription>
					</EmptyHeader>
				</Empty>
			)}

			{!isLoading && maps.length > 0 && <MapGrid maps={maps} />}

			{!isLoading && totalPages > 1 && (
				<Pagination className="mt-6">
					<PaginationContent>
						<PaginationItem>
							<PaginationPrevious
								aria-disabled={p <= 1}
								className={
									p <= 1 ? "pointer-events-none opacity-50" : "cursor-pointer"
								}
								onClick={() => goToPage(Math.max(1, p - 1))}
							/>
						</PaginationItem>

						{pageItems(p, totalPages).map((item) => (
							<PaginationItem key={item.key}>
								{item.page === null ? (
									<PaginationEllipsis />
								) : (
									<PaginationLink
										className="cursor-pointer"
										isActive={item.page === p}
										onClick={() => goToPage(item.page as number)}
									>
										{item.page}
									</PaginationLink>
								)}
							</PaginationItem>
						))}

						<PaginationItem>
							<PaginationNext
								aria-disabled={p >= totalPages}
								className={
									p >= totalPages
										? "pointer-events-none opacity-50"
										: "cursor-pointer"
								}
								onClick={() => goToPage(Math.min(totalPages, p + 1))}
							/>
						</PaginationItem>
					</PaginationContent>
				</Pagination>
			)}
		</AppShell>
	);
}

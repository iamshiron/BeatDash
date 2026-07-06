import {
	MagnifyingGlassIcon,
	MusicNotesSimpleIcon,
} from "@phosphor-icons/react";
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
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import { useDebouncedCallback } from "@tanstack/react-pacer";
import { keepPreviousData } from "@tanstack/react-query";
import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";
import { memo, useEffect, useState } from "react";
import { useGetApiMaps } from "@/api/maps/maps";
import type { MapListItemDto } from "@/api/model";
import { AppShell } from "@/components/layout/AppShell";
import { MapCard } from "@/components/maps/MapCard";

const PAGE_SIZE = 24;
const SEARCH_DEBOUNCE_MS = 300;

type MapsSearch = { q?: string; p?: number };

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

export const Route = createFileRoute("/maps/")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	validateSearch: (search: Record<string, unknown>): MapsSearch => ({
		q: typeof search.q === "string" ? search.q : "",
		p: Math.max(1, Number(search.p) || 1),
	}),
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
	const { q = "", p = 1 } = Route.useSearch();
	const navigate = useNavigate();

	// The box tracks keystrokes immediately; the URL (and thus the query) is only
	// updated once typing settles, so the grid isn't refetched on every character.
	const [input, setInput] = useState(q);
	useEffect(() => setInput(q), [q]);

	const commitSearch = useDebouncedCallback(
		(value: string) => {
			navigate({ to: "/maps", search: { q: value, p: 1 } });
		},
		{ wait: SEARCH_DEBOUNCE_MS },
	);

	const { data, isLoading } = useGetApiMaps(
		{ Page: p, PageSize: PAGE_SIZE, Search: q.trim() || undefined },
		{ query: { placeholderData: keepPreviousData } },
	);

	const result = data?.status === 200 ? data.data : undefined;
	const maps = result?.items ?? [];
	const totalPages = result ? Number(result.totalPages) : 0;

	const goToPage = (page: number) =>
		navigate({ to: "/maps", search: { q, p: page } });

	return (
		<AppShell wide>
			<div className="flex items-center justify-between gap-3">
				<h1 className="font-heading text-lg font-semibold tracking-tight">
					Maps
				</h1>
				<div className="relative w-full max-w-xs">
					<MagnifyingGlassIcon className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
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

			{isLoading && (
				<div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
					{["skel-a", "skel-b", "skel-c", "skel-d", "skel-e", "skel-f"].map(
						(key) => (
							<Skeleton key={key} className="h-28 rounded-xl" />
						),
					)}
				</div>
			)}

			{!isLoading && maps.length === 0 && (
				<Empty className="mt-10">
					<EmptyHeader>
						<EmptyMedia variant="icon">
							<MusicNotesSimpleIcon />
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

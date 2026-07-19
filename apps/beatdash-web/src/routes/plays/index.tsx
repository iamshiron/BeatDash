import { Badge } from "@shiron/ui/components/ui/badge";
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
	ClockCircleIcon,
	CloseCircleIcon,
	MagnifierIcon,
} from "@solar-icons/react/dynamic";
import {
	createFileRoute,
	Link,
	redirect,
	useNavigate,
} from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { useGetApiSessions } from "@/api/sessions/sessions";
import { AppShell } from "@/components/layout/AppShell";
import { SessionCard } from "@/components/sessions/SessionCard";
import { SessionFilters } from "@/components/sessions/SessionFilters";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";
import {
	FILTER_KEYS,
	getActiveFilters,
	hasActiveFilters,
	parseSessionSearch,
	type SessionSearchParams,
	SORT_OPTIONS_COMBINED,
	toApiParams,
} from "@/lib/sessions";

export const Route = createFileRoute("/plays/")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	validateSearch: parseSessionSearch,
	component: SessionsListPage,
});

const SKELETON_KEYS = Array.from(
	{ length: 6 },
	(_, i) => `session-skeleton-${i}`,
);

/** First/last plus a window around the current page, with nulls marking gaps. */
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

function SessionsListPage() {
	useDocumentTitle("Plays");
	const search = Route.useSearch();
	const navigate = useNavigate();

	const [inputValue, setInputValue] = useState(search.q ?? "");

	useEffect(() => {
		const timer = setTimeout(() => {
			if (inputValue !== search.q) {
				navigate({
					to: "/plays",
					search: { ...search, q: inputValue, page: 1 },
				});
			}
		}, 300);
		return () => clearTimeout(timer);
	}, [inputValue, navigate, search]);

	function updateSearch(updates: Partial<SessionSearchParams>) {
		navigate({
			to: "/plays",
			search: { ...search, ...updates, q: inputValue },
		});
	}

	// Any filter change also returns to the first page.
	function applyFilters(updates: Partial<SessionSearchParams>) {
		updateSearch({ ...updates, page: 1 });
	}

	// Clears the given filter keys (used by the removable chips).
	function clearFilters(keys: (keyof SessionSearchParams)[]) {
		applyFilters(Object.fromEntries(keys.map((k) => [k, undefined])));
	}

	function clearAllFilters() {
		clearFilters([...FILTER_KEYS]);
	}

	const params = toApiParams(search);
	const { data, isLoading } = useGetApiSessions(params);

	const result = data?.status === 200 ? data.data : null;
	const sessions = result?.items ?? [];
	const totalCount = result ? Number(result.totalCount) : 0;
	const totalPages = result ? Number(result.totalPages) : 0;
	const page = result ? Number(result.page) : (search.page ?? 1);

	const activeFilters = getActiveFilters(search);
	const hasFilters = Boolean(search.q) || hasActiveFilters(search);
	const isGenuinelyEmpty = !isLoading && totalCount === 0 && !hasFilters;

	return (
		<AppShell wide>
			<div className="flex flex-wrap items-end justify-between gap-3">
				<div>
					<div className="mb-2 inline-flex items-center gap-1 rounded-lg border border-border bg-muted/30 p-0.5">
						<Button
							variant="secondary"
							size="sm"
							className="h-7 px-2.5 text-xs"
						>
							Plays
						</Button>
						<Button
							variant="ghost"
							size="sm"
							className="h-7 px-2.5 text-xs"
							asChild
						>
							<Link to="/plays/liked">Liked maps</Link>
						</Button>
					</div>
					<h1 className="font-heading text-xl font-bold tracking-tight">
						Plays
					</h1>
					<p className="mt-0.5 text-xs text-muted-foreground">
						{totalCount} {totalCount === 1 ? "play" : "plays"}
					</p>
				</div>
				<div className="flex flex-wrap items-center gap-2">
					<div className="relative w-full min-w-48 max-w-xs flex-1">
						<MagnifierIcon className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
						<Input
							value={inputValue}
							onChange={(e) => setInputValue(e.target.value)}
							placeholder="Search…"
							className="h-9 pl-8"
						/>
					</div>

					<Select
						value={`${search.sort ?? "StartedAt"}:${search.dir ?? "Desc"}`}
						onValueChange={(v) => {
							const [sort, dir] = v.split(":") as [string, string];
							updateSearch({ sort, dir, page: 1 });
						}}
					>
						<SelectTrigger className="h-9! w-[10rem] text-xs">
							<SelectValue />
						</SelectTrigger>
						<SelectContent>
							{SORT_OPTIONS_COMBINED.map((opt) => (
								<SelectItem key={opt.value} value={opt.value}>
									{opt.label}
								</SelectItem>
							))}
						</SelectContent>
					</Select>

					<SessionFilters
						search={search}
						onChange={applyFilters}
						onReset={clearAllFilters}
					/>
				</div>
			</div>

			{activeFilters.length > 0 && (
				<div className="mt-3 flex flex-wrap items-center gap-1.5">
					{activeFilters.map((filter) => (
						<Badge
							key={filter.id}
							variant="secondary"
							className="gap-1 py-1 pl-2.5 pr-1 text-xs font-normal"
						>
							{filter.label}
							<button
								type="button"
								aria-label={`Remove ${filter.label} filter`}
								onClick={() => clearFilters(filter.keys)}
								className="grid size-4 place-content-center rounded-full text-muted-foreground transition-colors hover:bg-foreground/10 hover:text-foreground"
							>
								<CloseCircleIcon className="size-3" />
							</button>
						</Badge>
					))}
					<Button
						type="button"
						variant="ghost"
						size="sm"
						className="h-6 px-2 text-xs text-muted-foreground"
						onClick={clearAllFilters}
					>
						Clear all
					</Button>
				</div>
			)}

			{isLoading && (
				<div className="mt-3 flex flex-col gap-2">
					{SKELETON_KEYS.map((key) => (
						<div
							key={key}
							className="flex items-center gap-3.5 rounded-xl border border-border p-3"
						>
							<Skeleton className="size-14 shrink-0 rounded-lg" />
							<div className="flex-1 space-y-2">
								<Skeleton className="h-3.5 w-48" />
								<Skeleton className="h-3 w-32" />
							</div>
							<Skeleton className="h-10 w-16 shrink-0" />
						</div>
					))}
				</div>
			)}

			{!isLoading && sessions.length === 0 && (
				<Empty className="mt-10">
					<EmptyHeader>
						<EmptyMedia variant="icon">
							<ClockCircleIcon />
						</EmptyMedia>
						<EmptyTitle>
							{isGenuinelyEmpty ? "No plays yet" : "No plays found"}
						</EmptyTitle>
						<EmptyDescription>
							{isGenuinelyEmpty
								? "Play a map on your headset to see plays here."
								: "Try adjusting your filters or search term."}
						</EmptyDescription>
					</EmptyHeader>
				</Empty>
			)}

			{!isLoading && sessions.length > 0 && (
				<div className="mt-3 flex flex-col gap-2">
					{sessions.map((session) => (
						<SessionCard key={session.id} session={session} />
					))}
				</div>
			)}

			{!isLoading && totalPages > 1 && (
				<Pagination className="mt-6">
					<PaginationContent>
						<PaginationItem>
							<PaginationPrevious
								aria-disabled={page <= 1}
								className={
									page <= 1
										? "pointer-events-none opacity-50"
										: "cursor-pointer"
								}
								onClick={() => updateSearch({ page: page - 1 })}
							/>
						</PaginationItem>

						{pageItems(page, totalPages).map((item) => (
							<PaginationItem key={item.key}>
								{item.page === null ? (
									<PaginationEllipsis />
								) : (
									<PaginationLink
										className="cursor-pointer"
										isActive={item.page === page}
										onClick={() => updateSearch({ page: item.page as number })}
									>
										{item.page}
									</PaginationLink>
								)}
							</PaginationItem>
						))}

						<PaginationItem>
							<PaginationNext
								aria-disabled={page >= totalPages}
								className={
									page >= totalPages
										? "pointer-events-none opacity-50"
										: "cursor-pointer"
								}
								onClick={() => updateSearch({ page: page + 1 })}
							/>
						</PaginationItem>
					</PaginationContent>
				</Pagination>
			)}
		</AppShell>
	);
}

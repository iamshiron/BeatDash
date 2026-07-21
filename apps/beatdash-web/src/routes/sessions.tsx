import {
	Empty,
	EmptyDescription,
	EmptyHeader,
	EmptyMedia,
	EmptyTitle,
} from "@shiron/ui/components/ui/empty";
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
import { LayersIcon, SortVerticalIcon } from "@solar-icons/react/dynamic";
import { keepPreviousData } from "@tanstack/react-query";
import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";
import { useGetSittings } from "@/api/sessions/sessions";
import { ErrorState } from "@/components/common/ErrorState";
import { AppShell } from "@/components/layout/AppShell";
import { SittingCard } from "@/components/sessions/SittingCard";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";
import {
	normalizeSittingSort,
	SITTING_SORT_OPTIONS,
	type SittingSortValue,
	sittingSortToApi,
} from "@/lib/sittings";

const PAGE_SIZE = 20;

type SessionsSearch = { p?: number; sort?: SittingSortValue };

export const Route = createFileRoute("/sessions")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	validateSearch: (search: Record<string, unknown>): SessionsSearch => ({
		p: Math.max(1, Number(search.p) || 1),
		sort: normalizeSittingSort(
			typeof search.sort === "string" ? search.sort : undefined,
		),
	}),
	component: SessionsPage,
});

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

const SKELETON_KEYS = ["a", "b", "c", "d", "e"];

function SessionsPage() {
	useDocumentTitle("Sessions");
	const { p = 1, sort = "newest" } = Route.useSearch();
	const navigate = useNavigate();

	const { data, isLoading, isError, refetch } = useGetSittings(
		{ page: p, pageSize: PAGE_SIZE, sortBy: sittingSortToApi(sort) },
		{ query: { placeholderData: keepPreviousData } },
	);
	const result = data?.status === 200 ? data.data : undefined;
	const sittings = result?.items ?? [];
	const totalPages = result ? Number(result.totalPages) : 0;
	const hasError = isError || (data != null && data.status >= 500);

	const goToPage = (page: number) =>
		navigate({ to: "/sessions", search: { p: page, sort } });

	const changeSort = (value: SittingSortValue) =>
		// A new order re-pages from the top, so reset to page 1.
		navigate({ to: "/sessions", search: { p: 1, sort: value } });

	return (
		<AppShell wide>
			<div className="mb-4 flex flex-wrap items-end justify-between gap-3">
				<div>
					<h1 className="font-heading text-lg font-semibold tracking-tight">
						Sessions
					</h1>
					<p className="mt-1 text-sm text-muted-foreground">
						Your plays grouped into sittings. Click one to see every play in it.
					</p>
				</div>
				<Select value={sort} onValueChange={changeSort}>
					<SelectTrigger
						className="h-9 w-[9.5rem] gap-1.5"
						aria-label="Sort sessions"
					>
						<SortVerticalIcon className="size-4 text-muted-foreground" />
						<SelectValue />
					</SelectTrigger>
					<SelectContent align="end">
						{SITTING_SORT_OPTIONS.map((opt) => (
							<SelectItem key={opt.value} value={opt.value}>
								{opt.label}
							</SelectItem>
						))}
					</SelectContent>
				</Select>
			</div>

			{isLoading && (
				<div className="flex flex-col gap-3">
					{SKELETON_KEYS.map((key) => (
						<Skeleton key={key} className="h-16 rounded-xl" />
					))}
				</div>
			)}

			{!isLoading && hasError && (
				<ErrorState title="Couldn't load sessions" onRetry={() => refetch()} />
			)}

			{!isLoading && !hasError && sittings.length === 0 && (
				<Empty className="mt-10">
					<EmptyHeader>
						<EmptyMedia variant="icon">
							<LayersIcon />
						</EmptyMedia>
						<EmptyTitle>No sessions yet</EmptyTitle>
						<EmptyDescription>
							Play a few maps on your headset and they'll be grouped into
							sessions here.
						</EmptyDescription>
					</EmptyHeader>
				</Empty>
			)}

			{!isLoading && sittings.length > 0 && (
				<div className="flex flex-col gap-3">
					{sittings.map((sitting) => (
						<SittingCard
							key={`${sitting.startedAt}-${sitting.endedAt}`}
							sitting={sitting}
						/>
					))}
				</div>
			)}

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

import {
	ArrowDownIcon,
	ArrowUpIcon,
	CaretLeftIcon,
	CaretRightIcon,
	ClockIcon,
	MagnifyingGlassIcon,
	RobotIcon,
} from "@phosphor-icons/react";
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
	Select,
	SelectContent,
	SelectItem,
	SelectTrigger,
	SelectValue,
} from "@shiron/ui/components/ui/select";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { useGetApiSessions } from "@/api/sessions/sessions";
import { AppShell } from "@/components/layout/AppShell";
import { SessionCard } from "@/components/sessions/SessionCard";
import {
	DIFFICULTY_OPTIONS,
	type SessionSearchParams,
	SORT_OPTIONS,
	toApiParams,
} from "@/lib/sessions";

export const Route = createFileRoute("/sessions/")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	validateSearch: (search: Record<string, unknown>): SessionSearchParams => ({
		page: Math.max(1, Number(search.page) || 1),
		q: typeof search.q === "string" ? search.q : "",
		difficulty:
			typeof search.difficulty === "string" && search.difficulty !== "all"
				? search.difficulty
				: undefined,
		sortBy: typeof search.sortBy === "string" ? search.sortBy : "StartedAt",
		sortDir: typeof search.sortDir === "string" ? search.sortDir : "Desc",
		includeAuto: search.includeAuto === "true" || search.includeAuto === true,
	}),
	component: SessionsListPage,
});

const SKELETON_KEYS = Array.from(
	{ length: 6 },
	(_, i) => `session-skeleton-${i}`,
);

function SessionsListPage() {
	const search = Route.useSearch();
	const navigate = useNavigate();

	const [inputValue, setInputValue] = useState(search.q ?? "");

	useEffect(() => {
		const timer = setTimeout(() => {
			if (inputValue !== search.q) {
				navigate({
					to: "/sessions",
					search: { ...search, q: inputValue, page: 1 },
				});
			}
		}, 300);
		return () => clearTimeout(timer);
	}, [inputValue, navigate, search]);

	function updateSearch(updates: Partial<SessionSearchParams>) {
		navigate({
			to: "/sessions",
			search: { ...search, ...updates, q: inputValue },
		});
	}

	const params = toApiParams(search);
	const { data, isLoading } = useGetApiSessions(params);

	const result = data?.status === 200 ? data.data : null;
	const sessions = result?.items ?? [];
	const totalCount = result ? Number(result.totalCount) : 0;
	const totalPages = result ? Number(result.totalPages) : 0;
	const page = result ? Number(result.page) : (search.page ?? 1);

	const hasFilters = search.q || search.difficulty;

	return (
		<AppShell wide>
			<div className="flex items-center justify-between gap-3">
				<h1 className="font-heading text-lg font-semibold tracking-tight">
					Sessions
				</h1>
				<div className="relative w-full max-w-xs">
					<MagnifyingGlassIcon className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
					<Input
						value={inputValue}
						onChange={(e) => setInputValue(e.target.value)}
						placeholder="Search sessions…"
						className="h-9 pl-8"
					/>
				</div>
			</div>

			<div className="mt-4 flex flex-wrap items-center gap-2">
				<Select
					value={search.difficulty ?? "all"}
					onValueChange={(v) =>
						updateSearch({
							difficulty: v === "all" ? undefined : v,
							page: 1,
						})
					}
				>
					<SelectTrigger className="h-9 w-36">
						<SelectValue placeholder="Difficulty" />
					</SelectTrigger>
					<SelectContent>
						<SelectItem value="all">All Difficulties</SelectItem>
						{DIFFICULTY_OPTIONS.map((opt) => (
							<SelectItem key={opt.value} value={opt.value}>
								{opt.label}
							</SelectItem>
						))}
					</SelectContent>
				</Select>

				<Select
					value={search.sortBy}
					onValueChange={(v) => updateSearch({ sortBy: v, page: 1 })}
				>
					<SelectTrigger className="h-9 w-36">
						<SelectValue placeholder="Sort by" />
					</SelectTrigger>
					<SelectContent>
						{SORT_OPTIONS.map((opt) => (
							<SelectItem key={opt.value} value={opt.value}>
								{opt.label}
							</SelectItem>
						))}
					</SelectContent>
				</Select>

				<Button
					variant="outline"
					size="icon"
					className="h-9 w-9"
					onClick={() =>
						updateSearch({
							sortDir: search.sortDir === "Asc" ? "Desc" : "Asc",
						})
					}
				>
					{search.sortDir === "Asc" ? <ArrowUpIcon /> : <ArrowDownIcon />}
				</Button>

				<Button
					variant={search.includeAuto ? "default" : "outline"}
					size="sm"
					className="h-9"
					onClick={() =>
						updateSearch({
							includeAuto: !search.includeAuto,
							page: 1,
						})
					}
				>
					<RobotIcon className="size-3.5" />
					Auto
				</Button>
			</div>

			{isLoading && (
				<div className="mt-4 flex flex-col gap-2">
					{SKELETON_KEYS.map((key) => (
						<Skeleton key={key} className="h-20 rounded-xl" />
					))}
				</div>
			)}

			{!isLoading && sessions.length === 0 && (
				<Empty className="mt-10">
					<EmptyHeader>
						<EmptyMedia variant="icon">
							<ClockIcon />
						</EmptyMedia>
						<EmptyTitle>
							{hasFilters ? "No sessions found" : "No sessions yet"}
						</EmptyTitle>
						<EmptyDescription>
							{hasFilters
								? "Try adjusting your filters or search term."
								: "Play a map on your headset to see sessions here."}
						</EmptyDescription>
					</EmptyHeader>
				</Empty>
			)}

			{!isLoading && sessions.length > 0 && (
				<div className="mt-4 flex flex-col gap-2">
					{sessions.map((session) => (
						<SessionCard key={session.id} session={session} />
					))}
				</div>
			)}

			{!isLoading && totalPages > 1 && (
				<div className="mt-6 flex items-center justify-between">
					<span className="text-xs text-muted-foreground">
						Page {page} of {totalPages} ({totalCount} sessions)
					</span>
					<div className="flex gap-2">
						<Button
							variant="outline"
							size="sm"
							disabled={page <= 1}
							onClick={() => updateSearch({ page: page - 1 })}
						>
							<CaretLeftIcon /> Prev
						</Button>
						<Button
							variant="outline"
							size="sm"
							disabled={page >= totalPages}
							onClick={() => updateSearch({ page: page + 1 })}
						>
							Next <CaretRightIcon />
						</Button>
					</div>
				</div>
			)}
		</AppShell>
	);
}

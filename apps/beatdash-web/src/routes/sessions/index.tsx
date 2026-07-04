import {
	CaretLeftIcon,
	CaretRightIcon,
	ClockIcon,
	MagnifyingGlassIcon,
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
import { Switch } from "@shiron/ui/components/ui/switch";
import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { useGetApiSessions } from "@/api/sessions/sessions";
import { AppShell } from "@/components/layout/AppShell";
import { SessionCard } from "@/components/sessions/SessionCard";
import {
	DIFFICULTY_OPTIONS,
	type SessionSearchParams,
	SORT_OPTIONS_COMBINED,
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
	const isGenuinelyEmpty = !isLoading && totalCount === 0 && !hasFilters;

	return (
		<AppShell wide>
			<div className="flex items-end justify-between gap-4">
				<div>
					<h1 className="font-heading text-xl font-bold tracking-tight">
						Sessions
					</h1>
					<p className="mt-0.5 text-xs text-muted-foreground">
						{totalCount} {totalCount === 1 ? "session" : "sessions"}
					</p>
				</div>
				<div className="relative w-full max-w-xs">
					<MagnifyingGlassIcon className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
					<Input
						value={inputValue}
						onChange={(e) => setInputValue(e.target.value)}
						placeholder="Search…"
						className="h-9 pl-8"
					/>
				</div>
			</div>

			<div className="mt-3 flex flex-wrap items-center gap-1.5 rounded-lg border border-border/60 bg-muted/20 p-1.5">
				<Select
					value={search.difficulty ?? "all"}
					onValueChange={(v) =>
						updateSearch({
							difficulty: v === "all" ? undefined : v,
							page: 1,
						})
					}
				>
					<SelectTrigger className="h-8 w-[7.5rem] border-transparent bg-background text-xs">
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

				<div className="h-4 w-px bg-border/60" />

				<Select
					value={`${search.sortBy ?? "StartedAt"}:${search.sortDir ?? "Desc"}`}
					onValueChange={(v) => {
						const [sortBy, sortDir] = v.split(":") as [string, string];
						updateSearch({ sortBy, sortDir, page: 1 });
					}}
				>
					<SelectTrigger className="h-8 w-[10rem] border-transparent bg-background text-xs">
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

				<div className="ml-auto flex items-center gap-1.5 text-xs text-muted-foreground">
					Auto
					<Switch
						checked={search.includeAuto}
						onCheckedChange={(checked) =>
							updateSearch({ includeAuto: checked, page: 1 })
						}
					/>
				</div>
			</div>

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
							<ClockIcon />
						</EmptyMedia>
						<EmptyTitle>
							{isGenuinelyEmpty ? "No sessions yet" : "No sessions found"}
						</EmptyTitle>
						<EmptyDescription>
							{isGenuinelyEmpty
								? "Play a map on your headset to see sessions here."
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
				<div className="mt-6 flex items-center justify-between">
					<p className="font-mono text-xs tabular-nums text-muted-foreground">
						{page} / {totalPages}
					</p>
					<div className="flex items-center gap-1.5">
						<Button
							variant="outline"
							size="icon"
							className="size-8"
							disabled={page <= 1}
							onClick={() => updateSearch({ page: page - 1 })}
						>
							<CaretLeftIcon className="size-4" />
						</Button>
						<Button
							variant="outline"
							size="icon"
							className="size-8"
							disabled={page >= totalPages}
							onClick={() => updateSearch({ page: page + 1 })}
						>
							<CaretRightIcon className="size-4" />
						</Button>
					</div>
				</div>
			)}
		</AppShell>
	);
}

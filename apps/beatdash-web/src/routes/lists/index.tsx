import { Button } from "@shiron/ui/components/ui/button";
import {
	Empty,
	EmptyDescription,
	EmptyHeader,
	EmptyMedia,
	EmptyTitle,
} from "@shiron/ui/components/ui/empty";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import {
	AddCircleIcon,
	MusicNotesIcon,
	PlaylistIcon,
} from "@solar-icons/react/dynamic";
import { createFileRoute, Link, redirect } from "@tanstack/react-router";
import { useState } from "react";
import { useGetApiLists } from "@/api/lists/lists";
import { getGetApiMapsMapIdCoverUrl } from "@/api/maps/maps";
import type { MapListSummaryDto } from "@/api/model";
import { AppShell } from "@/components/layout/AppShell";
import { CreateListDialog } from "@/components/lists/CreateListDialog";

import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export const Route = createFileRoute("/lists/")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	component: ListsPage,
});

const SKELETON_KEYS = Array.from({ length: 4 }, (_, i) => `list-skeleton-${i}`);

function ListsPage() {
	useDocumentTitle("Lists");
	const [createOpen, setCreateOpen] = useState(false);
	const { data, isLoading } = useGetApiLists();
	const lists = data?.status === 200 ? data.data : [];

	return (
		<AppShell wide>
			<div className="flex flex-wrap items-end justify-between gap-3">
				<div>
					<h1 className="font-heading text-xl font-bold tracking-tight">
						Lists
					</h1>
					<p className="mt-0.5 text-xs text-muted-foreground">
						{lists.length} {lists.length === 1 ? "list" : "lists"}
					</p>
				</div>
				<Button
					size="sm"
					className="gap-1.5"
					onClick={() => setCreateOpen(true)}
				>
					<AddCircleIcon className="size-4" />
					New list
				</Button>
			</div>

			{isLoading && (
				<div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
					{SKELETON_KEYS.map((key) => (
						<Skeleton key={key} className="h-32 rounded-xl" />
					))}
				</div>
			)}

			{!isLoading && lists.length === 0 && (
				<Empty className="mt-10">
					<EmptyHeader>
						<EmptyMedia variant="icon">
							<PlaylistIcon />
						</EmptyMedia>
						<EmptyTitle>No lists yet</EmptyTitle>
						<EmptyDescription>
							Create a list to group maps together — like "Warmup" or "Trying to
							Beat".
						</EmptyDescription>
					</EmptyHeader>
					<Button
						size="sm"
						className="mt-4 gap-1.5"
						onClick={() => setCreateOpen(true)}
					>
						<AddCircleIcon className="size-4" />
						New list
					</Button>
				</Empty>
			)}

			{!isLoading && lists.length > 0 && (
				<div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
					{lists.map((list) => (
						<ListCard key={list.id} list={list} />
					))}
				</div>
			)}

			<CreateListDialog open={createOpen} onOpenChange={setCreateOpen} />
		</AppShell>
	);
}

function ListCard({ list }: { list: MapListSummaryDto }) {
	const covers = list.coverMapIds.slice(0, 4);
	const mapCount = Number(list.mapCount);

	return (
		<Link
			to="/lists/$id"
			params={{ id: list.id }}
			className="flex flex-col overflow-hidden rounded-xl border border-border bg-card transition-colors hover:border-primary/40"
		>
			<div className="flex h-24 items-center gap-1 bg-gradient-to-br from-primary/20 to-[oklch(0.62_0.19_255)]/20 p-3">
				{covers.length > 0 ? (
					covers.map((id, i) => (
						<img
							key={id}
							src={getGetApiMapsMapIdCoverUrl(id)}
							alt=""
							loading="lazy"
							className="size-16 rounded-md object-cover shadow-sm"
							style={{ zIndex: covers.length - i }}
						/>
					))
				) : (
					<div className="flex size-full items-center justify-center">
						<PlaylistIcon className="size-8 text-muted-foreground/40" />
					</div>
				)}
			</div>
			<div className="flex flex-1 flex-col gap-1 p-3">
				<h2 className="truncate font-heading text-sm font-semibold">
					{list.name}
				</h2>
				{list.description && (
					<p className="line-clamp-2 text-xs text-muted-foreground">
						{list.description}
					</p>
				)}
				<div className="mt-auto flex items-center gap-1.5 pt-1 text-xs text-muted-foreground">
					<MusicNotesIcon className="size-3.5" />
					<span className="tabular-nums">
						{mapCount} {mapCount === 1 ? "map" : "maps"}
					</span>
				</div>
			</div>
		</Link>
	);
}

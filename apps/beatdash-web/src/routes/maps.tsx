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
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import { createFileRoute, redirect } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useGetApiMaps } from "@/api/maps/maps";
import { AppShell } from "@/components/layout/AppShell";
import { MapCard } from "@/components/maps/MapCard";

export const Route = createFileRoute("/maps")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	component: MapsPage,
});

function MapsPage() {
	const [query, setQuery] = useState("");
	const { data, isLoading } = useGetApiMaps();
	const maps = data?.status === 200 ? data.data : [];

	const filtered = useMemo(() => {
		const q = query.trim().toLowerCase();
		if (!q) return maps;
		return maps.filter((m) =>
			[m.songName, m.songAuthor, m.mapper]
				.filter(Boolean)
				.some((field) => field.toLowerCase().includes(q)),
		);
	}, [maps, query]);

	return (
		<AppShell wide>
			<div className="flex items-center justify-between gap-3">
				<h1 className="font-heading text-lg font-semibold tracking-tight">
					Maps
				</h1>
				<div className="relative w-full max-w-xs">
					<MagnifyingGlassIcon className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
					<Input
						value={query}
						onChange={(e) => setQuery(e.target.value)}
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

			{!isLoading && filtered.length === 0 && (
				<Empty className="mt-10">
					<EmptyHeader>
						<EmptyMedia variant="icon">
							<MusicNotesSimpleIcon />
						</EmptyMedia>
						<EmptyTitle>
							{query.trim() ? "No maps found" : "No maps yet"}
						</EmptyTitle>
						<EmptyDescription>
							{query.trim()
								? "Try a different search term."
								: "Play a map on your headset to see it here."}
						</EmptyDescription>
					</EmptyHeader>
				</Empty>
			)}

			{!isLoading && filtered.length > 0 && (
				<div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
					{filtered.map((map) => (
						<MapCard key={map.id} map={map} />
					))}
				</div>
			)}
		</AppShell>
	);
}

import { Badge } from "@shiron/ui/components/ui/badge";
import {
	Card,
	CardContent,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import {
	Tabs,
	TabsContent,
	TabsList,
	TabsTrigger,
} from "@shiron/ui/components/ui/tabs";
import { cn } from "@shiron/ui/lib/utils";
import { Cup } from "@solar-icons/react";
import { Link, useSearch } from "@tanstack/react-router";
import { formatDistanceToNow } from "date-fns";
import type {
	PlaySessionListItemDto,
	SessionTopDifficultyDto,
} from "@/api/model";
import {
	DIFFICULTY_TEXT_STYLES,
	formatAccuracy,
	formatScore,
	RANK_STYLES,
	type SessionSearchParams,
} from "@/lib/sessions";

const RANK_ACCENT = [
	"text-amber-300",
	"text-slate-300",
	"text-orange-400",
] as const;

export function TopSessions({
	difficulties,
	currentId,
}: {
	difficulties: SessionTopDifficultyDto[];
	currentId: string;
}) {
	const sessionSearch = useSearch({ from: "/plays/$id" });

	if (!difficulties.some((d) => d.sessions.length > 0)) return null;

	const defaultTab = (difficulties.find((d) => d.isCurrent) ?? difficulties[0])
		.beatmapDifficultyId;

	return (
		<Card>
			<CardHeader>
				<CardTitle className="flex items-center gap-2">
					<Cup className="size-4 text-amber-300" weight="Bold" />
					Top Sessions
				</CardTitle>
			</CardHeader>
			<CardContent>
				<Tabs defaultValue={defaultTab}>
					<TabsList className="w-full">
						{difficulties.map((d) => (
							<TabsTrigger
								key={d.beatmapDifficultyId}
								value={d.beatmapDifficultyId}
							>
								<span
									className={
										DIFFICULTY_TEXT_STYLES[d.difficultyRank] ??
										"text-foreground"
									}
								>
									{d.difficultyName}
								</span>
							</TabsTrigger>
						))}
					</TabsList>

					{difficulties.map((d) => (
						<TabsContent
							key={d.beatmapDifficultyId}
							value={d.beatmapDifficultyId}
							className="mt-3 space-y-1.5"
						>
							{d.sessions.length === 0 ? (
								<p className="py-6 text-center text-xs text-muted-foreground">
									No sessions on this difficulty yet
								</p>
							) : (
								d.sessions.map((session, index) => (
									<TopSessionRow
										key={session.id}
										session={session}
										rank={index}
										isCurrent={session.id === currentId}
										search={sessionSearch}
									/>
								))
							)}
						</TabsContent>
					))}
				</Tabs>
			</CardContent>
		</Card>
	);
}

function TopSessionRow({
	session,
	rank,
	isCurrent,
	search,
}: {
	session: PlaySessionListItemDto;
	rank: number;
	isCurrent: boolean;
	search: SessionSearchParams;
}) {
	const results = session.results;

	return (
		<Link
			to="/plays/$id"
			params={{ id: session.id }}
			search={search}
			className={cn(
				"flex items-center gap-3 rounded-lg border px-3 py-2 transition-colors",
				isCurrent
					? "border-primary/40 bg-primary/5"
					: "border-transparent hover:border-border hover:bg-accent/30",
			)}
		>
			<span
				className={cn(
					"w-6 shrink-0 text-center font-heading text-sm font-bold tabular-nums",
					RANK_ACCENT[rank] ?? "text-muted-foreground",
				)}
			>
				{rank + 1}
			</span>

			<div className="min-w-0 flex-1">
				<div className="flex items-center gap-1.5">
					<span className="font-mono text-sm font-medium tabular-nums">
						{results ? formatScore(results.score) : "—"}
					</span>
					{results?.fullCombo && (
						<span className="text-[10px] font-bold uppercase tracking-wide text-amber-400">
							FC
						</span>
					)}
					{isCurrent && (
						<Badge
							variant="secondary"
							className="h-4 shrink-0 px-1.5 py-0 text-[10px]"
						>
							This session
						</Badge>
					)}
				</div>
				<span className="text-[10px] text-muted-foreground/60">
					{formatDistanceToNow(new Date(session.startedAt), {
						addSuffix: true,
					})}
				</span>
			</div>

			{results && (
				<div className="flex shrink-0 items-center gap-3">
					<span className="font-mono text-[10px] tabular-nums text-muted-foreground">
						{formatAccuracy(results.accuracy)} · {Number(results.maxCombo)}x
					</span>
					<span
						className={cn(
							"w-6 text-right font-heading text-base font-bold leading-none",
							RANK_STYLES[results.rank] ?? "text-muted-foreground",
						)}
					>
						{results.rank}
					</span>
				</div>
			)}
		</Link>
	);
}

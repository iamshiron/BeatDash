import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import {
	ClockCircleIcon,
	LayersIcon,
	MusicNotesIcon,
	RepeatIcon,
} from "@solar-icons/react/dynamic";
import { useGetSittingsOverview } from "@/api/sessions/sessions";
import { AnimatedNumber } from "@/components/common/AnimatedNumber";
import { StatTile } from "@/components/profile/StatTile";
import { formatScore } from "@/lib/sessions";

/** Formats a millisecond span as `Xh Ym` (or `Ym` under an hour). */
function formatActiveTime(ms: number): string {
	const totalMinutes = Math.floor(ms / 60000);
	const hours = Math.floor(totalMinutes / 60);
	const minutes = totalMinutes % 60;
	return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`;
}

/** Formats an ISO instant as a short "last played" sub-label, e.g. `Last played Jul 17`. */
function formatLastPlayed(iso: string | null): string | undefined {
	if (!iso) return undefined;
	return `Last played ${new Date(iso).toLocaleDateString(undefined, {
		month: "short",
		day: "numeric",
	})}`;
}

const SKELETON_KEYS = ["a", "b", "c", "d"];

/**
 * A compact totals strip above the sessions list: how many sittings the player has,
 * their total plays and active time, and the average plays per session. Backed by a
 * hydration-free aggregate endpoint, so it loads independently of the paged list.
 */
export function SessionsOverview() {
	const { data, isLoading } = useGetSittingsOverview();
	const overview = data?.status === 200 ? data.data : undefined;

	if (isLoading) {
		return (
			<div className="mb-5 grid grid-cols-2 gap-3 sm:grid-cols-4">
				{SKELETON_KEYS.map((key) => (
					<Skeleton key={key} className="h-[5.25rem] rounded-xl" />
				))}
			</div>
		);
	}

	// Nothing to summarize yet — the list's own empty state carries the message.
	if (!overview || Number(overview.totalSessions) === 0) return null;

	return (
		<div className="mb-5 grid grid-cols-2 gap-3 sm:grid-cols-4">
			<StatTile
				icon={<LayersIcon className="size-4" />}
				label="Sessions"
				value={
					<AnimatedNumber
						value={Number(overview.totalSessions)}
						format={(n) => formatScore(Math.round(n))}
					/>
				}
			/>
			<StatTile
				icon={<MusicNotesIcon className="size-4" />}
				label="Total plays"
				value={
					<AnimatedNumber
						value={Number(overview.totalPlays)}
						format={(n) => formatScore(Math.round(n))}
					/>
				}
			/>
			<StatTile
				icon={<ClockCircleIcon className="size-4" />}
				label="Active time"
				value={
					<AnimatedNumber
						value={Number(overview.totalActiveMs)}
						format={formatActiveTime}
					/>
				}
			/>
			<StatTile
				icon={<RepeatIcon className="size-4" />}
				label="Avg / session"
				value={
					<AnimatedNumber
						value={Number(overview.avgPlaysPerSession)}
						format={(n) => n.toFixed(1)}
					/>
				}
				sub={formatLastPlayed(overview.lastPlayedAt)}
			/>
		</div>
	);
}

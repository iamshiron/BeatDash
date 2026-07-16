import { Spinner } from "@shiron/ui/components/ui/spinner";
import { cn } from "@shiron/ui/lib/utils";
import { PauseIcon, PlayIcon } from "@solar-icons/react/dynamic";
import { type PlayerTrack, usePlayer } from "@/contexts/player";

/**
 * Compact circular play/pause button that loads a map's song into the global bottom
 * player. Sized to overlay a cover thumbnail; swallows the click so it never triggers
 * an enclosing card link. Reflects the shared player's state when this map is current.
 */
export function SongPlayButton({
	track,
	className,
	revealOnHover = false,
}: {
	track: PlayerTrack;
	className?: string;
	/**
	 * When true, the button stays hidden until the enclosing `group` is hovered/focused
	 * — except while this track is playing or loading, so pausing is always reachable.
	 */
	revealOnHover?: boolean;
}) {
	const player = usePlayer();

	const isCurrent = player.track?.mapId === track.mapId;
	const isPlaying = isCurrent && player.isPlaying;
	const isLoading = isCurrent && player.isLoading;
	const active = isPlaying || isLoading;

	return (
		<button
			type="button"
			onClick={(e) => {
				e.preventDefault();
				e.stopPropagation();
				player.playTrack(track);
			}}
			aria-label={isPlaying ? "Pause preview" : "Play song preview"}
			title={isPlaying ? "Pause" : "Play song preview"}
			className={cn(
				"flex size-8 items-center justify-center rounded-full bg-black/55 text-white shadow-sm backdrop-blur-sm transition hover:bg-black/75 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/70 [&_svg]:size-4",
				revealOnHover &&
					!active &&
					"opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 focus-visible:opacity-100",
				className,
			)}
		>
			{isLoading ? (
				<Spinner className="size-4" />
			) : isPlaying ? (
				<PauseIcon weight="Bold" />
			) : (
				<PlayIcon weight="Bold" />
			)}
		</button>
	);
}

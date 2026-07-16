import { Slider } from "@shiron/ui/components/ui/slider";
import { Spinner } from "@shiron/ui/components/ui/spinner";
import {
	CloseCircleIcon,
	MusicNotesIcon,
	PauseIcon,
	PlayIcon,
	VolumeCrossIcon,
	VolumeLoudIcon,
	VolumeSmallIcon,
} from "@solar-icons/react/dynamic";
import { Link } from "@tanstack/react-router";
import { getGetApiMapsMapIdCoverUrl } from "@/api/maps/maps";
import { usePlayer } from "@/contexts/player";

function formatTime(seconds: number): string {
	if (!Number.isFinite(seconds) || seconds < 0) return "0:00";
	const total = Math.floor(seconds);
	return `${Math.floor(total / 60)}:${(total % 60).toString().padStart(2, "0")}`;
}

/**
 * Persistent "now playing" bar docked to the bottom of the viewport. Renders only
 * while a track is loaded and drives the shared player: play/pause, a seekable
 * progress track, elapsed / total time, and a volume slider with mute.
 */
export function BottomPlayer() {
	const {
		track,
		isPlaying,
		isLoading,
		hasError,
		currentTime,
		duration,
		volume,
		muted,
		toggle,
		seek,
		setVolume,
		toggleMute,
		close,
	} = usePlayer();

	if (!track) return null;

	const VolumeIcon =
		muted || volume === 0
			? VolumeCrossIcon
			: volume < 0.5
				? VolumeSmallIcon
				: VolumeLoudIcon;

	return (
		<div className="pointer-events-none fixed inset-x-0 bottom-4 z-50 mx-auto w-full max-w-3xl px-4">
			<div className="glass pointer-events-auto flex items-center gap-3 rounded-2xl border border-border p-2.5 pr-3 shadow-lg">
				{/* Cover */}
				<Link
					to="/maps/$id"
					params={{ id: track.mapId }}
					className="flex size-11 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-gradient-to-br from-primary/40 to-[oklch(0.62_0.19_255)]/40"
				>
					{track.coverImageKey ? (
						<img
							src={getGetApiMapsMapIdCoverUrl(track.mapId)}
							alt={track.songName}
							className="size-full object-cover"
						/>
					) : (
						<MusicNotesIcon className="size-5 text-muted-foreground/50" />
					)}
				</Link>

				{/* Play / pause */}
				<button
					type="button"
					onClick={toggle}
					aria-label={isPlaying ? "Pause" : "Play"}
					className="flex size-9 shrink-0 items-center justify-center rounded-full bg-primary text-primary-foreground transition hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring [&_svg]:size-4.5"
				>
					{isLoading ? (
						<Spinner className="size-4.5" />
					) : isPlaying ? (
						<PauseIcon weight="Bold" />
					) : (
						<PlayIcon weight="Bold" />
					)}
				</button>

				{/* Title + progress */}
				<div className="flex min-w-0 flex-1 flex-col gap-1">
					<div className="flex min-w-0 items-center gap-2">
						<span className="min-w-0 flex-1 truncate text-sm font-medium">
							{track.songName}
						</span>
						<span className="shrink-0 font-mono text-[11px] tabular-nums text-muted-foreground">
							{hasError
								? "unavailable"
								: `${formatTime(currentTime)} / ${duration > 0 ? formatTime(duration) : "--:--"}`}
						</span>
					</div>
					<Slider
						aria-label="Seek"
						min={0}
						max={duration > 0 ? duration : 1}
						step={0.1}
						value={[Math.min(currentTime, duration > 0 ? duration : 1)]}
						disabled={duration <= 0 || hasError}
						onValueChange={([v]) => seek(v)}
						className="w-full [&_[data-slot=slider-thumb]]:size-3 [&_[data-slot=slider-track]]:data-horizontal:h-1.5"
					/>
				</div>

				{/* Volume */}
				<div className="hidden items-center gap-1.5 sm:flex">
					<button
						type="button"
						onClick={toggleMute}
						aria-label={muted ? "Unmute" : "Mute"}
						className="flex size-8 shrink-0 items-center justify-center rounded-md text-muted-foreground transition hover:bg-muted hover:text-foreground [&_svg]:size-4.5"
					>
						<VolumeIcon weight="Bold" />
					</button>
					<Slider
						aria-label="Volume"
						min={0}
						max={1}
						step={0.01}
						value={[muted ? 0 : volume]}
						onValueChange={([v]) => setVolume(v)}
						className="w-20 [&_[data-slot=slider-thumb]]:size-3 [&_[data-slot=slider-track]]:data-horizontal:h-1.5"
					/>
				</div>

				{/* Close */}
				<button
					type="button"
					onClick={close}
					aria-label="Close player"
					className="flex size-8 shrink-0 items-center justify-center rounded-md text-muted-foreground transition hover:bg-muted hover:text-foreground [&_svg]:size-5"
				>
					<CloseCircleIcon weight="Bold" />
				</button>
			</div>
		</div>
	);
}

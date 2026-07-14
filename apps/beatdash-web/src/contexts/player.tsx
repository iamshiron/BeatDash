import {
	createContext,
	type ReactNode,
	useCallback,
	useContext,
	useEffect,
	useRef,
	useState,
} from "react";
import { getGetApiMapsMapIdSongUrl } from "@/api/maps/maps";

/** The map whose song is loaded into the global player. */
export interface PlayerTrack {
	mapId: string;
	songName: string;
	songAuthor: string;
	coverImageKey: string | null;
}

export interface PlayerValue {
	track: PlayerTrack | null;
	isPlaying: boolean;
	isLoading: boolean;
	hasError: boolean;
	/** Current playback position in seconds. */
	currentTime: number;
	/** Track length in seconds, or 0 until metadata loads. */
	duration: number;
	/** Output volume in [0, 1]. */
	volume: number;
	muted: boolean;
	/** Loads and plays a track, or toggles play/pause when it is already current. */
	playTrack: (track: PlayerTrack) => void;
	/** Toggles play/pause of the current track. */
	toggle: () => void;
	seek: (time: number) => void;
	setVolume: (volume: number) => void;
	toggleMute: () => void;
	/** Stops playback and dismisses the player. */
	close: () => void;
}

const PlayerContext = createContext<PlayerValue | null>(null);

const VOLUME_KEY = "beatdash.player.volume";

function readStoredVolume(): number {
	if (typeof window === "undefined") return 1;
	const raw = window.localStorage.getItem(VOLUME_KEY);
	const parsed = raw == null ? Number.NaN : Number.parseFloat(raw);
	return Number.isFinite(parsed) ? Math.min(1, Math.max(0, parsed)) : 1;
}

/**
 * Owns the single `<audio>` element behind the app's persistent bottom player, so a
 * song keeps playing across route changes and only one track is ever audible.
 */
export function PlayerProvider({ children }: { children: ReactNode }) {
	const audioRef = useRef<HTMLAudioElement | null>(null);
	const [track, setTrack] = useState<PlayerTrack | null>(null);
	const [isPlaying, setIsPlaying] = useState(false);
	const [isLoading, setIsLoading] = useState(false);
	const [hasError, setHasError] = useState(false);
	const [currentTime, setCurrentTime] = useState(0);
	const [duration, setDuration] = useState(0);
	const [volume, setVolumeState] = useState(readStoredVolume);
	const [muted, setMuted] = useState(false);

	// Create the audio element once and mirror its events into React state.
	useEffect(() => {
		const audio = new Audio();
		audio.preload = "metadata";
		audio.volume = readStoredVolume();
		audioRef.current = audio;

		const onPlay = () => setIsPlaying(true);
		const onPause = () => setIsPlaying(false);
		const onPlaying = () => setIsLoading(false);
		const onWaiting = () => setIsLoading(true);
		const onEnded = () => {
			setIsPlaying(false);
			setCurrentTime(0);
		};
		const onTimeUpdate = () => setCurrentTime(audio.currentTime);
		const onLoadedMetadata = () =>
			setDuration(Number.isFinite(audio.duration) ? audio.duration : 0);
		const onError = () => {
			setHasError(true);
			setIsLoading(false);
			setIsPlaying(false);
		};
		const onVolumeChange = () => {
			setVolumeState(audio.volume);
			setMuted(audio.muted);
		};

		audio.addEventListener("play", onPlay);
		audio.addEventListener("pause", onPause);
		audio.addEventListener("playing", onPlaying);
		audio.addEventListener("waiting", onWaiting);
		audio.addEventListener("ended", onEnded);
		audio.addEventListener("timeupdate", onTimeUpdate);
		audio.addEventListener("loadedmetadata", onLoadedMetadata);
		audio.addEventListener("error", onError);
		audio.addEventListener("volumechange", onVolumeChange);

		return () => {
			audio.pause();
			audio.removeAttribute("src");
			audio.load();
			audio.removeEventListener("play", onPlay);
			audio.removeEventListener("pause", onPause);
			audio.removeEventListener("playing", onPlaying);
			audio.removeEventListener("waiting", onWaiting);
			audio.removeEventListener("ended", onEnded);
			audio.removeEventListener("timeupdate", onTimeUpdate);
			audio.removeEventListener("loadedmetadata", onLoadedMetadata);
			audio.removeEventListener("error", onError);
			audio.removeEventListener("volumechange", onVolumeChange);
			audioRef.current = null;
		};
	}, []);

	const playTrack = useCallback(
		(next: PlayerTrack) => {
			const audio = audioRef.current;
			if (!audio) return;

			// Same track already loaded — just toggle play/pause.
			if (track?.mapId === next.mapId) {
				if (audio.paused) {
					setHasError(false);
					setIsLoading(true);
					audio.play().catch(() => {
						setHasError(true);
						setIsLoading(false);
					});
				} else {
					audio.pause();
				}
				return;
			}

			setTrack(next);
			setHasError(false);
			setCurrentTime(0);
			setDuration(0);
			setIsLoading(true);
			audio.src = getGetApiMapsMapIdSongUrl(next.mapId);
			audio.play().catch(() => {
				setHasError(true);
				setIsLoading(false);
			});
		},
		[track],
	);

	const toggle = useCallback(() => {
		const audio = audioRef.current;
		if (!audio || !track) return;
		if (audio.paused) {
			setHasError(false);
			setIsLoading(true);
			audio.play().catch(() => {
				setHasError(true);
				setIsLoading(false);
			});
		} else {
			audio.pause();
		}
	}, [track]);

	const seek = useCallback((time: number) => {
		const audio = audioRef.current;
		if (audio && Number.isFinite(time)) {
			audio.currentTime = Math.max(0, time);
			setCurrentTime(audio.currentTime);
		}
	}, []);

	const setVolume = useCallback((next: number) => {
		const audio = audioRef.current;
		const clamped = Math.min(1, Math.max(0, next));
		if (audio) {
			audio.volume = clamped;
			if (clamped > 0 && audio.muted) audio.muted = false;
		}
		setVolumeState(clamped);
		if (typeof window !== "undefined") {
			window.localStorage.setItem(VOLUME_KEY, String(clamped));
		}
	}, []);

	const toggleMute = useCallback(() => {
		const audio = audioRef.current;
		if (!audio) return;
		audio.muted = !audio.muted;
		setMuted(audio.muted);
	}, []);

	const close = useCallback(() => {
		const audio = audioRef.current;
		if (audio) {
			audio.pause();
			audio.removeAttribute("src");
			audio.load();
		}
		setTrack(null);
		setIsPlaying(false);
		setIsLoading(false);
		setHasError(false);
		setCurrentTime(0);
		setDuration(0);
	}, []);

	const value: PlayerValue = {
		track,
		isPlaying,
		isLoading,
		hasError,
		currentTime,
		duration,
		volume,
		muted,
		playTrack,
		toggle,
		seek,
		setVolume,
		toggleMute,
		close,
	};

	return (
		<PlayerContext.Provider value={value}>{children}</PlayerContext.Provider>
	);
}

export function usePlayer(): PlayerValue {
	const ctx = useContext(PlayerContext);
	if (!ctx) {
		throw new Error("usePlayer must be used within a PlayerProvider");
	}
	return ctx;
}

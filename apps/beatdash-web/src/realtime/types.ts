/**
 * TypeScript interfaces mirroring the C# event records in BeatDash.Data.Realtime.Events.
 * Keep these in sync with the backend DTOs.
 */

/** Mirrors `Shiron.BeatDash.Data.Realtime.Events.DeviceStatusEvent`. */
export interface DeviceStatusEvent {
	clientId: string;
	isOnline: boolean;
	timestamp: string;
}

/** Mirrors `Shiron.BeatDash.Data.Realtime.Events.DevicePairedEvent`. */
export interface DevicePairedEvent {
	clientId: string;
	name: string;
	timestamp: string;
}

/** Mirrors `Shiron.BeatDash.Data.Realtime.Events.LiveMapStartedEvent`. */
export interface LiveMapStartedEvent {
	mapId: string | null;
	songName: string;
	songSubName: string;
	songAuthor: string;
	mapper: string;
	bpm: number;
	durationMs: number;
	difficulty: string;
	difficultyName: string;
	notesPerSecond: number;
	noteJumpSpeed: number | null;
	bombCount: number;
	obstacleCount: number;
	cuttableObjectCount: number;
	laneCount: number;
	characteristic: string;
	timestamp: string;
}

/** Mirrors `Shiron.BeatDash.Data.Realtime.Events.ScoreUpdateEvent`. */
export interface ScoreUpdateEvent {
	correlationId: number;
	songTime: number;
	score: number;
	maxScore: number;
	accuracy: number;
	rank: string;
	energy: number;
	combo: number;
	misses: number;
	timestamp: string;
}

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

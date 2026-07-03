import type {
	DevicePairedEvent,
	DeviceStatusEvent,
	LiveMapStartedEvent,
} from "./types";

/**
 * Type-safe map of SignalR event names to their payload types.
 *
 * The keys MUST match the camelCase wire format of the `IRealtimeClient`
 * method names on the backend (ASP.NET Core SignalR serializes method names
 * as camelCase by default).
 *
 * To add a new event:
 * 1. Add the payload interface to `./types.ts`
 * 2. Add the mapping here
 */
export interface RealtimeEvents {
	receiveDeviceStatus: DeviceStatusEvent;
	receiveDevicePaired: DevicePairedEvent;
	receiveLiveMapStarted: LiveMapStartedEvent;
}

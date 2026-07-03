import { useEffect, useRef } from "react";
import type { RealtimeEvents } from "./events";
import { useRealtimeContext } from "./RealtimeProvider";

/**
 * Type-safe hook for subscribing to a SignalR event.
 *
 * The handler is stored in a ref, so it always receives the latest closure
 * without triggering a re-subscription. The subscription is automatically
 * set up when the connection becomes available and torn down on unmount.
 *
 * @example
 * ```tsx
 * useRealtimeEvent("receiveDeviceStatus", (event) => {
 *     if (event.clientId === targetId) {
 *         setIsOnline(event.isOnline);
 *     }
 * });
 * ```
 */
export function useRealtimeEvent<K extends keyof RealtimeEvents>(
	eventName: K,
	handler: (payload: RealtimeEvents[K]) => void,
): void {
	const callbackRef = useRef(handler);
	callbackRef.current = handler;

	const { connection } = useRealtimeContext();

	useEffect(() => {
		if (!connection) return;

		const listener = (payload: RealtimeEvents[K]) => {
			callbackRef.current(payload);
		};

		connection.on(eventName, listener);

		return () => {
			connection.off(eventName, listener);
		};
	}, [connection, eventName]);
}

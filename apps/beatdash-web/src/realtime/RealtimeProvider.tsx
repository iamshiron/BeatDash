import { type HubConnection, HubConnectionBuilder } from "@microsoft/signalr";
import {
	createContext,
	type ReactNode,
	useContext,
	useEffect,
	useState,
} from "react";
import { useAuth } from "@/contexts/auth";

const HUB_URL = "/api/client/web";

const RECONNECT_DELAYS = [0, 2000, 5000, 10000, 30000];

export type RealtimeConnectionState =
	| "connecting"
	| "connected"
	| "reconnecting"
	| "disconnected";

export interface RealtimeContextValue {
	connection: HubConnection | null;
	connectionState: RealtimeConnectionState;
}

const RealtimeContext = createContext<RealtimeContextValue | null>(null);

export function RealtimeProvider({ children }: { children: ReactNode }) {
	const { isAuthenticated } = useAuth();
	const [connection, setConnection] = useState<HubConnection | null>(null);
	const [connectionState, setConnectionState] =
		useState<RealtimeConnectionState>("disconnected");

	useEffect(() => {
		if (!isAuthenticated) {
			setConnection(null);
			setConnectionState("disconnected");
			return;
		}

		const conn = new HubConnectionBuilder()
			.withUrl(HUB_URL)
			.withAutomaticReconnect(RECONNECT_DELAYS)
			.configureLogging("warning")
			.build();

		conn.onreconnecting(() => setConnectionState("reconnecting"));
		conn.onreconnected(() => setConnectionState("connected"));
		conn.onclose(() => setConnectionState("disconnected"));

		setConnection(conn);
		setConnectionState("connecting");

		conn
			.start()
			.then(() => setConnectionState("connected"))
			.catch(() => setConnectionState("disconnected"));

		return () => {
			setConnection(null);
			setConnectionState("disconnected");
			conn.stop().catch(() => {});
		};
	}, [isAuthenticated]);

	return (
		<RealtimeContext.Provider value={{ connection, connectionState }}>
			{children}
		</RealtimeContext.Provider>
	);
}

export function useRealtimeContext(): RealtimeContextValue {
	const ctx = useContext(RealtimeContext);
	if (!ctx) {
		throw new Error(
			"useRealtimeContext must be used within a RealtimeProvider",
		);
	}
	return ctx;
}

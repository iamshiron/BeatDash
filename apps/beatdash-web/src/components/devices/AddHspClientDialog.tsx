import { Button } from "@shiron/ui/components/ui/button";
import {
	Dialog,
	DialogClose,
	DialogContent,
	DialogDescription,
	DialogFooter,
	DialogHeader,
	DialogTitle,
} from "@shiron/ui/components/ui/dialog";
import { Input } from "@shiron/ui/components/ui/input";
import { Spinner } from "@shiron/ui/components/ui/spinner";
import { CopyIcon, HeartPulseIcon } from "@solar-icons/react/dynamic";
import { QRCodeSVG } from "qrcode.react";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { generateHspToken } from "@/api/hsp/hsp";
import { useGetApiServer } from "@/api/server/server";

// Metrics advertised to the client for provisioning (mirrors the backend HspMetrics registry).
const METRICS = ["heart_rate", "calories", "steps", "spo2"] as const;

/**
 * Links a Honami Health Proxy client: mints a fresh push token and renders a QR (plus a
 * copyable URL + token fallback) the companion app scans to start pushing sensor samples.
 * Minting rotates the token, so a new code disconnects any previously linked client.
 */
export function AddHspClientDialog({
	open,
	onOpenChange,
}: {
	open: boolean;
	onOpenChange: (open: boolean) => void;
}) {
	const [token, setToken] = useState<string | null>(null);
	const [isLoading, setIsLoading] = useState(false);
	const [error, setError] = useState(false);

	useEffect(() => {
		if (!open) {
			setToken(null);
			setError(false);
			setIsLoading(false);
		}
	}, [open]);

	const {
		data: serverResponse,
		isLoading: serverLoading,
		isError: serverError,
	} = useGetApiServer({ query: { enabled: open } });

	const serverInfo = serverResponse?.data;
	const address = serverInfo
		? `${serverInfo.hostAddress}:${serverInfo.apiPort}`
		: "";
	const ingestUrl = serverInfo
		? `http://${address}/api/hsp/ingest`
		: "";

	const payload =
		token && ingestUrl
			? JSON.stringify({
					v: 1,
					name: "BeatDash",
					ingest: ingestUrl,
					auth: "token",
					token,
					metrics: METRICS,
				})
			: null;

	const handleGenerate = () => {
		setIsLoading(true);
		setError(false);
		generateHspToken()
			.then((response) => {
				if (response.status === 200) setToken(response.data.token);
				else setError(true);
			})
			.catch(() => setError(true))
			.finally(() => setIsLoading(false));
	};

	const copy = (value: string, label: string) => {
		if (!value) return;
		navigator.clipboard
			.writeText(value)
			.then(() => toast.success(`${label} copied.`))
			.catch(() => toast.error(`Could not copy the ${label.toLowerCase()}.`));
	};

	return (
		<Dialog open={open} onOpenChange={onOpenChange}>
			<DialogContent className="sm:max-w-lg">
				<DialogHeader>
					<DialogTitle className="flex items-center gap-2">
						<HeartPulseIcon className="size-5 text-primary" weight="Bold" />
						Link a Honami Health Proxy client
					</DialogTitle>
					<DialogDescription>
						Scan this code in the Honami companion app to push heart rate and other
						sensor data from your watch into BeatDash.
					</DialogDescription>
				</DialogHeader>

				{!token && (
					<div className="flex flex-col gap-4">
						<p className="text-sm text-muted-foreground">
							Generate a pairing code, then open the Honami app and scan it to add
							BeatDash as a destination. Generating a new code disconnects any
							client you linked before.
						</p>
						{error && (
							<p className="text-center text-sm text-destructive">
								Failed to generate a pairing code. Please try again.
							</p>
						)}
						<Button onClick={handleGenerate} disabled={isLoading}>
							{isLoading ? <Spinner className="size-4" /> : null}
							{isLoading ? "Generating…" : "Generate pairing code"}
						</Button>
					</div>
				)}

				{token && (
					<div className="flex flex-col gap-4">
						<div className="flex justify-center">
							{payload ? (
								<div className="rounded-xl bg-white p-4">
									<QRCodeSVG value={payload} size={200} marginSize={0} />
								</div>
							) : serverError ? (
								<p className="py-8 text-center text-sm text-destructive">
									Generated a token but couldn't read the server address to build
									the code.
								</p>
							) : (
								<div className="flex items-center justify-center py-16">
									<Spinner className="size-6" />
								</div>
							)}
						</div>

						<div className="flex flex-col gap-1.5">
							<span className="text-sm font-medium text-foreground">
								Ingest URL
							</span>
							<div className="flex items-center gap-2">
								<Input
									readOnly
									value={serverLoading ? "Loading…" : ingestUrl}
									className="font-mono text-xs"
									onFocus={(e) => e.currentTarget.select()}
									aria-label="Ingest URL"
								/>
								<Button
									variant="outline"
									size="icon-sm"
									onClick={() => copy(ingestUrl, "Ingest URL")}
									disabled={!ingestUrl}
									aria-label="Copy ingest URL"
								>
									<CopyIcon />
								</Button>
							</div>
						</div>

						<div className="flex flex-col gap-1.5">
							<span className="text-sm font-medium text-foreground">Token</span>
							<div className="flex items-center gap-2">
								<Input
									readOnly
									value={token}
									className="font-mono text-xs"
									onFocus={(e) => e.currentTarget.select()}
									aria-label="Push token"
								/>
								<Button
									variant="outline"
									size="icon-sm"
									onClick={() => copy(token, "Token")}
									aria-label="Copy token"
								>
									<CopyIcon />
								</Button>
							</div>
							<p className="text-xs text-muted-foreground">
								Shown once. If scanning fails, paste the URL and token into the
								app manually.
							</p>
						</div>
					</div>
				)}

				<DialogFooter>
					<DialogClose asChild>
						<Button variant="outline">Done</Button>
					</DialogClose>
				</DialogFooter>
			</DialogContent>
		</Dialog>
	);
}

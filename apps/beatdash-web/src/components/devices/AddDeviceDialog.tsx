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
import {
	AltArrowLeftIcon,
	CopyIcon,
	DisplayIcon,
	GlassesIcon,
} from "@solar-icons/react/dynamic";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { getApiDeviceRegister } from "@/api/device/device";
import { useGetApiServer } from "@/api/server/server";
import type { RegisterDeviceResponseDto } from "@/api/model";

type DeviceMode = "pcvr" | "standalone";

interface ModeOption {
	value: DeviceMode;
	title: string;
	description: string;
	icon: typeof DisplayIcon;
}

const MODE_OPTIONS: readonly ModeOption[] = [
	{
		value: "pcvr",
		title: "PCVR",
		description:
			"The game runs on the same PC as the BeatDash server — for example a Valve Index, or when streaming to a headset.",
		icon: DisplayIcon,
	},
	{
		value: "standalone",
		title: "Standalone VR",
		description:
			"The game runs directly on the headset — for example a Meta Quest 3 or a similar standalone VR device.",
		icon: GlassesIcon,
	},
];

export function AddDeviceDialog({
	open,
	onOpenChange,
}: {
	open: boolean;
	onOpenChange: (open: boolean) => void;
}) {
	const [mode, setMode] = useState<DeviceMode | null>(null);
	const [pinData, setPinData] = useState<RegisterDeviceResponseDto | null>(
		null,
	);
	const [isLoading, setIsLoading] = useState(false);
	const [error, setError] = useState(false);
	const [timeLeft, setTimeLeft] = useState(0);

	useEffect(() => {
		if (!open) {
			setMode(null);
			setPinData(null);
			setError(false);
			setTimeLeft(0);
		}
	}, [open]);

	useEffect(() => {
		if (!mode) return;

		let cancelled = false;
		setIsLoading(true);
		setError(false);
		setPinData(null);

		getApiDeviceRegister()
			.then((response) => {
				if (cancelled) return;
				if (response.status === 200) {
					setPinData(response.data);
				} else {
					setError(true);
				}
			})
			.catch(() => {
				if (!cancelled) setError(true);
			})
			.finally(() => {
				if (!cancelled) setIsLoading(false);
			});

		return () => {
			cancelled = true;
		};
	}, [mode]);

	useEffect(() => {
		if (!pinData) return;

		const expiresAt = new Date(pinData.expires).getTime();
		const update = () => {
			setTimeLeft(Math.max(0, expiresAt - Date.now()));
		};
		update();
		const interval = setInterval(update, 1000);
		return () => clearInterval(interval);
	}, [pinData]);

	// Both modes need the API port the server listens on; PCVR only differs by
	// using the loopback host instead of the queried LAN address.
	const {
		data: serverResponse,
		isLoading: serverLoading,
		isError: serverError,
	} = useGetApiServer({
		query: { enabled: open && mode !== null },
	});

	const minutes = Math.floor(timeLeft / 60000);
	const seconds = Math.floor((timeLeft % 60000) / 1000);

	const serverInfo = serverResponse?.data;
	const host = mode === "pcvr" ? "127.0.0.1" : (serverInfo?.hostAddress ?? "");
	const address = serverInfo ? `${host}:${serverInfo.apiPort}` : "";
	const addressLoading = mode !== null && serverLoading;
	const addressError = mode !== null && serverError;

	const handleCopyAddress = () => {
		if (!address) return;
		navigator.clipboard
			.writeText(address)
			.then(() => toast.success("Server address copied."))
			.catch(() => toast.error("Could not copy the address."));
	};

	return (
		<Dialog open={open} onOpenChange={onOpenChange}>
			<DialogContent className="sm:max-w-lg">
				<DialogHeader>
					<DialogTitle className="flex items-center gap-2">
						{mode && (
							<Button
								variant="outline"
								size="icon-sm"
								className="-ml-1"
								onClick={() => setMode(null)}
								aria-label="Back to device type"
							>
								<AltArrowLeftIcon />
							</Button>
						)}
						Pair a device
					</DialogTitle>
					<DialogDescription>
						{mode
							? "Enter this code and server address in the BeatDash mod on your device."
							: "Where does the game run? Pick how you play to get the right setup."}
					</DialogDescription>
				</DialogHeader>

				{!mode && (
					<div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
						{MODE_OPTIONS.map((option) => (
							<button
								key={option.value}
								type="button"
								onClick={() => setMode(option.value)}
								className="group flex flex-col items-start gap-3 rounded-lg bg-card p-4 text-left ring-1 ring-foreground/10 transition-colors outline-none hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring"
							>
								<span className="flex size-10 items-center justify-center rounded-md bg-primary/10 text-primary transition-colors group-hover:bg-primary/15">
									<option.icon className="size-5" weight="Bold" />
								</span>
								<span className="font-heading text-sm font-medium">
									{option.title}
								</span>
								<span className="text-xs/relaxed text-muted-foreground">
									{option.description}
								</span>
							</button>
						))}
					</div>
				)}

				{mode && (
					<div className="flex flex-col gap-4">
						{isLoading && (
							<div className="flex items-center justify-center py-8">
								<Spinner className="size-6" />
							</div>
						)}

						{error && !isLoading && (
							<p className="py-4 text-center text-destructive">
								Failed to generate a pairing code. Please try again.
							</p>
						)}

						{pinData && !isLoading && (
							<div className="flex flex-col items-center gap-3 py-2">
								<div className="flex items-center justify-center gap-2.5">
									{pinData.pin.split("").map((digit, i) => (
										<div
											// biome-ignore lint/suspicious/noArrayIndexKey: PIN digits are static
											key={`pin-${i}`}
											className="flex size-12 items-center justify-center rounded-[35%] border border-input bg-input/20 font-heading text-2xl font-bold text-foreground dark:bg-input/30"
										>
											{digit}
										</div>
									))}
								</div>
								<p className="text-xs text-muted-foreground">
									{timeLeft > 0
										? `Expires in ${minutes}:${seconds.toString().padStart(2, "0")}`
										: "Code expired"}
								</p>
							</div>
						)}

						<div className="flex flex-col gap-1.5">
							<span className="font-medium text-foreground">
								Server address
							</span>
							<div className="flex items-center gap-2">
								<Input
									readOnly
									value={
										addressLoading
											? "Loading…"
											: addressError
												? "Unavailable"
												: address
									}
									className="font-mono"
									onFocus={(e) => e.currentTarget.select()}
									aria-label="Server address"
								/>
								<Button
									variant="outline"
									size="icon-sm"
									onClick={handleCopyAddress}
									disabled={!address}
									aria-label="Copy server address"
								>
									<CopyIcon />
								</Button>
							</div>
							<p className="text-xs text-muted-foreground">
								{addressError
									? "Could not reach the server to read its address."
									: "Set this address in the BeatDash mod settings on your device."}
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

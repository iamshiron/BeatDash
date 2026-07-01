import { useEffect, useState } from "react";
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
import { Spinner } from "@shiron/ui/components/ui/spinner";
import { getApiDeviceRegister } from "@/api/device/device";
import type { RegisterDeviceResponseDto } from "@/api/model";

export function AddDeviceDialog({
	open,
	onOpenChange,
}: {
	open: boolean;
	onOpenChange: (open: boolean) => void;
}) {
	const [pinData, setPinData] = useState<RegisterDeviceResponseDto | null>(
		null,
	);
	const [isLoading, setIsLoading] = useState(false);
	const [error, setError] = useState(false);
	const [timeLeft, setTimeLeft] = useState(0);

	useEffect(() => {
		if (!open) {
			setPinData(null);
			setError(false);
			setTimeLeft(0);
			return;
		}

		let cancelled = false;
		setIsLoading(true);
		setError(false);

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
	}, [open]);

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

	const minutes = Math.floor(timeLeft / 60000);
	const seconds = Math.floor((timeLeft % 60000) / 1000);

	return (
		<Dialog open={open} onOpenChange={onOpenChange}>
			<DialogContent>
				<DialogHeader>
					<DialogTitle>Pair a device</DialogTitle>
					<DialogDescription>
						Enter this code on your VR headset to pair it with your account.
					</DialogDescription>
				</DialogHeader>

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
								// biome-ignore lint/suspicious/noArrayIndexKey: PIN digits are static
								<div
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

				<DialogFooter>
					<DialogClose asChild>
						<Button variant="outline">Done</Button>
					</DialogClose>
				</DialogFooter>
			</DialogContent>
		</Dialog>
	);
}

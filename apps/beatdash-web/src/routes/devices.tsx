import {
	AlertDialog,
	AlertDialogAction,
	AlertDialogCancel,
	AlertDialogContent,
	AlertDialogDescription,
	AlertDialogFooter,
	AlertDialogHeader,
	AlertDialogTitle,
} from "@shiron/ui/components/ui/alert-dialog";
import { Badge } from "@shiron/ui/components/ui/badge";
import { Button } from "@shiron/ui/components/ui/button";
import {
	Card,
	CardAction,
	CardContent,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import {
	Dialog,
	DialogClose,
	DialogContent,
	DialogDescription,
	DialogFooter,
	DialogHeader,
	DialogTitle,
} from "@shiron/ui/components/ui/dialog";
import {
	Empty,
	EmptyContent,
	EmptyDescription,
	EmptyHeader,
	EmptyMedia,
	EmptyTitle,
} from "@shiron/ui/components/ui/empty";
import { Field, FieldGroup, FieldLabel } from "@shiron/ui/components/ui/field";
import { Input } from "@shiron/ui/components/ui/input";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import { StatusDot } from "@shiron/ui/components/ui/status-dot";
import {
	AddCircleIcon,
	MonitorIcon,
	PenIcon,
	TrashBinMinimalisticIcon,
} from "@solar-icons/react/dynamic";
import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, redirect } from "@tanstack/react-router";
import { formatDistanceToNow } from "date-fns";
import { useState } from "react";
import { toast } from "sonner";
import {
	getGetApiDeviceQueryKey,
	useDeleteApiDeviceClientId,
	useGetApiDevice,
	usePatchApiDeviceClientId,
} from "@/api/device/device";
import type { DeviceResponseDto } from "@/api/model";
import { AddDeviceDialog } from "@/components/devices/AddDeviceDialog";
import { AppShell } from "@/components/layout/AppShell";
import { useNow } from "@/hooks/useNow";
import { useRealtimeEvent } from "@/realtime";

export const Route = createFileRoute("/devices")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	component: DevicesPage,
});

function DevicesPage() {
	const [pairDialogOpen, setPairDialogOpen] = useState(false);
	const [renameTarget, setRenameTarget] = useState<DeviceResponseDto | null>(
		null,
	);
	const [deleteTarget, setDeleteTarget] = useState<DeviceResponseDto | null>(
		null,
	);

	const { data, isLoading } = useGetApiDevice();
	const devices = data?.status === 200 ? data.data : [];

	const queryClient = useQueryClient();
	useRealtimeEvent("receiveDeviceStatus", (event) => {
		const device = devices.find((d) => d.clientId === event.clientId);
		const name = device?.name ?? "Device";
		if (event.isOnline) {
			toast.success(`${name} came online`);
		} else {
			toast.info(`${name} went offline`);
		}
		queryClient.invalidateQueries({
			queryKey: getGetApiDeviceQueryKey(),
		});
	});
	useRealtimeEvent("receiveDevicePaired", () => {
		setPairDialogOpen(false);
		queryClient.invalidateQueries({
			queryKey: getGetApiDeviceQueryKey(),
		});
		toast.success("Device paired successfully.");
	});

	return (
		<AppShell>
			<div className="flex items-center justify-between">
				<h1 className="font-heading text-lg font-semibold tracking-tight">
					Devices
				</h1>
				<Button size="sm" onClick={() => setPairDialogOpen(true)}>
					<AddCircleIcon />
					Pair a Device
				</Button>
			</div>

			{isLoading && (
				<div className="mt-6 flex flex-col gap-3">
					<Skeleton className="h-20 rounded-lg" />
					<Skeleton className="h-20 rounded-lg" />
				</div>
			)}

			{!isLoading && devices.length === 0 && (
				<Empty className="mt-10">
					<EmptyHeader>
						<EmptyMedia variant="icon">
							<MonitorIcon />
						</EmptyMedia>
						<EmptyTitle>No devices paired</EmptyTitle>
						<EmptyDescription>
							Pair your VR headset to start syncing your Beat Saber sessions.
						</EmptyDescription>
					</EmptyHeader>
					<EmptyContent>
						<Button onClick={() => setPairDialogOpen(true)}>
							<AddCircleIcon />
							Pair a Device
						</Button>
					</EmptyContent>
				</Empty>
			)}

			{!isLoading && devices.length > 0 && (
				<div className="mt-6 flex flex-col gap-3">
					{devices.map((device) => (
						<DeviceRow
							key={device.clientId}
							device={device}
							onRename={() => setRenameTarget(device)}
							onDelete={() => setDeleteTarget(device)}
						/>
					))}
				</div>
			)}

			<AddDeviceDialog open={pairDialogOpen} onOpenChange={setPairDialogOpen} />

			<RenameDialog
				device={renameTarget}
				onClose={() => setRenameTarget(null)}
			/>

			<DeleteDialog
				device={deleteTarget}
				onClose={() => setDeleteTarget(null)}
			/>
		</AppShell>
	);
}

function DeviceRow({
	device,
	onRename,
	onDelete,
}: {
	device: DeviceResponseDto;
	onRename: () => void;
	onDelete: () => void;
}) {
	const session = device.session;
	const isOnline = session != null;
	useNow();
	const lastSeen = formatDistanceToNow(new Date(device.lastSeenAt), {
		addSuffix: true,
	});
	const statusText =
		isOnline && session
			? `Online for ${formatDistanceToNow(new Date(session.onlineSince), { addSuffix: false })}`
			: `Last seen ${lastSeen}`;

	return (
		<Card>
			<CardHeader>
				<div className="flex items-center gap-2">
					<StatusDot
						status={isOnline ? "online" : "offline"}
						pulse={isOnline}
					/>
					<CardTitle>{device.name}</CardTitle>
				</div>
				<CardAction>
					<Badge variant={isOnline ? "default" : "secondary"}>
						{isOnline ? "Online" : "Offline"}
					</Badge>
				</CardAction>
			</CardHeader>
			<CardContent className="flex items-center justify-between">
				<div className="flex flex-col gap-0.5 text-muted-foreground">
					<span>{statusText}</span>
					<span className="font-mono text-[0.625rem] opacity-60">
						{device.clientId}
					</span>
				</div>
				<div className="flex gap-1">
					<Button
						variant="outline"
						size="icon"
						onClick={onRename}
						aria-label="Rename device"
					>
						<PenIcon />
					</Button>
					<Button
						variant="outline"
						size="icon"
						onClick={onDelete}
						aria-label="Remove device"
					>
						<TrashBinMinimalisticIcon />
					</Button>
				</div>
			</CardContent>
		</Card>
	);
}

function RenameDialog({
	device,
	onClose,
}: {
	device: DeviceResponseDto | null;
	onClose: () => void;
}) {
	const [name, setName] = useState("");
	const queryClient = useQueryClient();

	const renameMutation = usePatchApiDeviceClientId({
		mutation: {
			onSuccess: async (response) => {
				if (response.status !== 204) {
					toast.error("Failed to rename device.");
					return;
				}
				await queryClient.invalidateQueries({
					queryKey: getGetApiDeviceQueryKey(),
				});
				toast.success("Device renamed.");
				onClose();
			},
		},
	});

	function handleOpen(isOpen: boolean) {
		if (isOpen && device) {
			setName(device.name);
		}
		if (!isOpen) {
			onClose();
		}
	}

	function handleSubmit(event: React.FormEvent) {
		event.preventDefault();
		if (!device || !name.trim()) return;
		renameMutation.mutate({
			clientId: device.clientId,
			data: { name: name.trim() },
		});
	}

	return (
		<Dialog open={device != null} onOpenChange={handleOpen}>
			<DialogContent>
				<DialogHeader>
					<DialogTitle>Rename device</DialogTitle>
					<DialogDescription>
						Give this device a name you will recognize.
					</DialogDescription>
				</DialogHeader>
				<form id="rename-form" onSubmit={handleSubmit}>
					<FieldGroup>
						<Field>
							<FieldLabel htmlFor="device-name">Name</FieldLabel>
							<Input
								id="device-name"
								value={name}
								onChange={(e) => setName(e.target.value)}
								required
								maxLength={32}
							/>
						</Field>
					</FieldGroup>
				</form>
				<DialogFooter>
					<DialogClose asChild>
						<Button variant="outline">Cancel</Button>
					</DialogClose>
					<Button
						type="submit"
						form="rename-form"
						disabled={renameMutation.isPending}
					>
						{renameMutation.isPending ? "Saving…" : "Save"}
					</Button>
				</DialogFooter>
			</DialogContent>
		</Dialog>
	);
}

function DeleteDialog({
	device,
	onClose,
}: {
	device: DeviceResponseDto | null;
	onClose: () => void;
}) {
	const queryClient = useQueryClient();

	const deleteMutation = useDeleteApiDeviceClientId({
		mutation: {
			onSuccess: async (response) => {
				if (response.status !== 204) {
					toast.error("Failed to remove device.");
					return;
				}
				await queryClient.invalidateQueries({
					queryKey: getGetApiDeviceQueryKey(),
				});
				toast.success("Device removed.");
				onClose();
			},
		},
	});

	return (
		<AlertDialog
			open={device != null}
			onOpenChange={(isOpen) => !isOpen && onClose()}
		>
			<AlertDialogContent>
				<AlertDialogHeader>
					<AlertDialogTitle>Remove device?</AlertDialogTitle>
					<AlertDialogDescription>
						{device?.name} will need to be paired again to reconnect. This
						cannot be undone.
					</AlertDialogDescription>
				</AlertDialogHeader>
				<AlertDialogFooter>
					<AlertDialogCancel>Cancel</AlertDialogCancel>
					<AlertDialogAction
						variant="destructive"
						disabled={deleteMutation.isPending}
						onClick={() => {
							if (device) {
								deleteMutation.mutate({ clientId: device.clientId });
							}
						}}
					>
						{deleteMutation.isPending ? "Removing…" : "Remove"}
					</AlertDialogAction>
				</AlertDialogFooter>
			</AlertDialogContent>
		</AlertDialog>
	);
}

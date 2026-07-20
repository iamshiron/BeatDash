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
	HeartPulseIcon,
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
import {
	getListHspClientsQueryKey,
	useListHspClients,
	useRenameHspClient,
	useUnlinkHspClient,
} from "@/api/hsp/hsp";
import type { DeviceResponseDto, HspClientDto } from "@/api/model";
import { AddDeviceDialog } from "@/components/devices/AddDeviceDialog";
import { AddHspClientDialog } from "@/components/devices/AddHspClientDialog";
import { AppShell } from "@/components/layout/AppShell";
import { useAuth } from "@/contexts/auth";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";
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
	useDocumentTitle("Devices");
	const { user } = useAuth();
	const healthEnabled = Boolean(user?.healthTrackingEnabled);
	const [pairDialogOpen, setPairDialogOpen] = useState(false);
	const [hspDialogOpen, setHspDialogOpen] = useState(false);
	const [renameTarget, setRenameTarget] = useState<DeviceResponseDto | null>(
		null,
	);
	const [deleteTarget, setDeleteTarget] = useState<DeviceResponseDto | null>(
		null,
	);

	const { data, isLoading } = useGetApiDevice();
	const devices = data?.status === 200 ? data.data : [];

	const { data: hspData, isLoading: hspLoading } = useListHspClients({
		query: { enabled: healthEnabled, refetchInterval: 3000 },
	});
	const hspClients = hspData?.status === 200 ? hspData.data : [];
	const [hspDeleteTarget, setHspDeleteTarget] = useState<HspClientDto | null>(
		null,
	);
	const [hspRenameTarget, setHspRenameTarget] = useState<HspClientDto | null>(
		null,
	);

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
				<div className="flex gap-2">
					{healthEnabled && (
						<Button
							size="sm"
							variant="outline"
							onClick={() => setHspDialogOpen(true)}
						>
							<HeartPulseIcon />
							Link Health Proxy
						</Button>
					)}
					<Button size="sm" onClick={() => setPairDialogOpen(true)}>
						<AddCircleIcon />
						Pair a Device
					</Button>
				</div>
			</div>

			{(isLoading || (healthEnabled && hspLoading)) && (
				<div className="mt-6 flex flex-col gap-3">
					<Skeleton className="h-20 rounded-lg" />
					<Skeleton className="h-20 rounded-lg" />
				</div>
			)}

			{!isLoading &&
				!(healthEnabled && hspLoading) &&
				devices.length === 0 &&
				hspClients.length === 0 && (
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

			{!isLoading &&
				!(healthEnabled && hspLoading) &&
				(devices.length > 0 || hspClients.length > 0) && (
					<div className="mt-6 flex flex-col gap-3">
						{devices.map((device) => (
							<DeviceRow
								key={device.clientId}
								device={device}
								onRename={() => setRenameTarget(device)}
								onDelete={() => setDeleteTarget(device)}
							/>
						))}
						{hspClients.map((client) => (
							<HspClientRow
								key={client.id}
								client={client}
								onRename={() => setHspRenameTarget(client)}
								onDelete={() => setHspDeleteTarget(client)}
							/>
						))}
					</div>
				)}

			<AddDeviceDialog open={pairDialogOpen} onOpenChange={setPairDialogOpen} />

			{healthEnabled && (
				<AddHspClientDialog
					open={hspDialogOpen}
					onOpenChange={setHspDialogOpen}
				/>
			)}

			<RenameDialog
				device={renameTarget}
				onClose={() => setRenameTarget(null)}
			/>

			<DeleteDialog
				device={deleteTarget}
				onClose={() => setDeleteTarget(null)}
			/>

			<HspRenameDialog
				client={hspRenameTarget}
				onClose={() => setHspRenameTarget(null)}
			/>

			<HspDeleteDialog
				client={hspDeleteTarget}
				onClose={() => setHspDeleteTarget(null)}
			/>
		</AppShell>
	);
}

function HspClientRow({
	client,
	onRename,
	onDelete,
}: {
	client: HspClientDto;
	onRename: () => void;
	onDelete: () => void;
}) {
	useNow();
	const lastSeen = client.lastSeenAt
		? `Last active ${formatDistanceToNow(new Date(client.lastSeenAt), { addSuffix: true })}`
		: "Not connected yet";

	return (
		<Card>
			<CardHeader>
				<div className="flex items-center gap-2">
					<HeartPulseIcon className="size-4 text-primary" weight="Bold" />
					<CardTitle>{client.name}</CardTitle>
				</div>
				<CardAction>
					<Badge
						variant="outline"
						className="border-primary/30 bg-primary/10 text-primary"
					>
						HSP
					</Badge>
				</CardAction>
			</CardHeader>
			<CardContent className="flex items-center justify-between">
				<div className="flex flex-col gap-0.5 text-muted-foreground">
					<span>{lastSeen}</span>
					<span className="font-mono text-[0.625rem] opacity-60">
						{client.id}
					</span>
				</div>
				<div className="flex items-center gap-3">
					{client.lastHeartRate != null && (
						<div className="flex items-center gap-1.5 text-rose-400">
							<HeartPulseIcon className="size-4 animate-pulse" weight="Bold" />
							<span className="font-heading text-lg font-bold tabular-nums">
								{Math.round(Number(client.lastHeartRate))}
							</span>
							<span className="text-[10px] text-muted-foreground">bpm</span>
						</div>
					)}
					<Button
						variant="outline"
						size="icon"
						onClick={onRename}
						aria-label="Rename client"
					>
						<PenIcon />
					</Button>
					<Button
						variant="outline"
						size="icon"
						onClick={onDelete}
						aria-label="Unlink client"
					>
						<TrashBinMinimalisticIcon />
					</Button>
				</div>
			</CardContent>
		</Card>
	);
}

function HspRenameDialog({
	client,
	onClose,
}: {
	client: HspClientDto | null;
	onClose: () => void;
}) {
	const [name, setName] = useState("");
	const queryClient = useQueryClient();

	const renameMutation = useRenameHspClient({
		mutation: {
			onSuccess: async (response) => {
				if (response.status !== 204) {
					toast.error("Failed to rename the client.");
					return;
				}
				await queryClient.invalidateQueries({
					queryKey: getListHspClientsQueryKey(),
				});
				toast.success("Client renamed.");
				onClose();
			},
		},
	});

	function handleOpen(isOpen: boolean) {
		if (isOpen && client) setName(client.name);
		if (!isOpen) onClose();
	}

	function handleSubmit(event: React.FormEvent) {
		event.preventDefault();
		if (!client || !name.trim()) return;
		renameMutation.mutate({ id: client.id, data: { name: name.trim() } });
	}

	return (
		<Dialog open={client != null} onOpenChange={handleOpen}>
			<DialogContent>
				<DialogHeader>
					<DialogTitle>Rename client</DialogTitle>
					<DialogDescription>
						Give this Honami client a name you will recognize.
					</DialogDescription>
				</DialogHeader>
				<form id="hsp-rename-form" onSubmit={handleSubmit}>
					<FieldGroup>
						<Field>
							<FieldLabel htmlFor="hsp-client-name">Name</FieldLabel>
							<Input
								id="hsp-client-name"
								value={name}
								onChange={(e) => setName(e.target.value)}
								required
								maxLength={64}
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
						form="hsp-rename-form"
						disabled={renameMutation.isPending}
					>
						{renameMutation.isPending ? "Saving…" : "Save"}
					</Button>
				</DialogFooter>
			</DialogContent>
		</Dialog>
	);
}

function HspDeleteDialog({
	client,
	onClose,
}: {
	client: HspClientDto | null;
	onClose: () => void;
}) {
	const queryClient = useQueryClient();

	const unlinkMutation = useUnlinkHspClient({
		mutation: {
			onSuccess: async (response) => {
				if (response.status !== 204) {
					toast.error("Failed to unlink the client.");
					return;
				}
				await queryClient.invalidateQueries({
					queryKey: getListHspClientsQueryKey(),
				});
				toast.success("Client unlinked.");
				onClose();
			},
		},
	});

	return (
		<AlertDialog
			open={client != null}
			onOpenChange={(isOpen) => !isOpen && onClose()}
		>
			<AlertDialogContent>
				<AlertDialogHeader>
					<AlertDialogTitle>Unlink client?</AlertDialogTitle>
					<AlertDialogDescription>
						{client?.name} will stop being able to push sensor data. Its token is
						revoked immediately and cannot be reused.
					</AlertDialogDescription>
				</AlertDialogHeader>
				<AlertDialogFooter>
					<AlertDialogCancel>Cancel</AlertDialogCancel>
					<AlertDialogAction
						variant="destructive"
						disabled={unlinkMutation.isPending}
						onClick={() => {
							if (client) unlinkMutation.mutate({ id: client.id });
						}}
					>
						{unlinkMutation.isPending ? "Unlinking…" : "Unlink"}
					</AlertDialogAction>
				</AlertDialogFooter>
			</AlertDialogContent>
		</AlertDialog>
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

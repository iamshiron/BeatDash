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
import { Button } from "@shiron/ui/components/ui/button";
import {
	Dialog,
	DialogContent,
	DialogDescription,
	DialogFooter,
	DialogHeader,
	DialogTitle,
} from "@shiron/ui/components/ui/dialog";
import {
	Empty,
	EmptyDescription,
	EmptyHeader,
	EmptyMedia,
	EmptyTitle,
} from "@shiron/ui/components/ui/empty";
import { Field, FieldGroup, FieldLabel } from "@shiron/ui/components/ui/field";
import { Input } from "@shiron/ui/components/ui/input";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import { Textarea } from "@shiron/ui/components/ui/textarea";
import {
	AltArrowLeftIcon,
	CloseCircleIcon,
	MusicNotesIcon,
	PenIcon,
	TrashBinMinimalisticIcon,
} from "@solar-icons/react/dynamic";
import { useQueryClient } from "@tanstack/react-query";
import {
	createFileRoute,
	Link,
	redirect,
	useNavigate,
} from "@tanstack/react-router";
import { useState } from "react";
import { toast } from "sonner";
import {
	getGetApiListsListIdQueryKey,
	getGetApiListsQueryKey,
	useDeleteApiListsListId,
	useDeleteApiListsListIdMapsMapId,
	useGetApiListsListId,
	usePatchApiListsListId,
} from "@/api/lists/lists";
import type { MapListItemDto } from "@/api/model";
import { AppShell } from "@/components/layout/AppShell";
import { MapCard } from "@/components/maps/MapCard";

import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export const Route = createFileRoute("/lists/$id")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	component: ListDetailPage,
});

const SKELETON_KEYS = Array.from({ length: 6 }, (_, i) => `map-skeleton-${i}`);

function ListDetailPage() {
	const { id } = Route.useParams();
	const navigate = useNavigate();
	const queryClient = useQueryClient();

	const [editOpen, setEditOpen] = useState(false);
	const [deleteOpen, setDeleteOpen] = useState(false);

	const { data, isLoading } = useGetApiListsListId(id);
	const list = data?.status === 200 ? data.data : undefined;
	useDocumentTitle(list?.name);
	const notFound = data?.status === 404;
	const maps = list?.maps ?? [];

	const deleteMutation = useDeleteApiListsListId({
		mutation: {
			onSuccess: async (res) => {
				if (res.status !== 204) {
					toast.error("Couldn't delete the list.");
					return;
				}
				await queryClient.invalidateQueries({
					queryKey: getGetApiListsQueryKey(),
				});
				toast.success("List deleted.");
				navigate({ to: "/lists" });
			},
			onError: () => toast.error("Couldn't delete the list."),
		},
	});

	return (
		<AppShell wide>
			<Button variant="ghost" size="sm" className="mb-3 gap-1.5 pl-2" asChild>
				<Link to="/lists">
					<AltArrowLeftIcon className="size-4" />
					Lists
				</Link>
			</Button>

			{isLoading && (
				<div className="space-y-4">
					<Skeleton className="h-8 w-48" />
					<div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
						{SKELETON_KEYS.map((key) => (
							<Skeleton key={key} className="h-28 rounded-xl" />
						))}
					</div>
				</div>
			)}

			{!isLoading && notFound && (
				<Empty className="mt-10">
					<EmptyHeader>
						<EmptyMedia variant="icon">
							<CloseCircleIcon />
						</EmptyMedia>
						<EmptyTitle>List not found</EmptyTitle>
						<EmptyDescription>
							This list doesn't exist or isn't yours.
						</EmptyDescription>
					</EmptyHeader>
				</Empty>
			)}

			{!isLoading && list && (
				<>
					<div className="flex flex-wrap items-start justify-between gap-3">
						<div className="min-w-0">
							<h1 className="font-heading text-xl font-bold tracking-tight">
								{list.name}
							</h1>
							{list.description && (
								<p className="mt-1 max-w-prose text-sm text-muted-foreground">
									{list.description}
								</p>
							)}
							<p className="mt-1 flex items-center gap-1.5 text-xs text-muted-foreground">
								<MusicNotesIcon className="size-3.5" />
								<span className="tabular-nums">
									{maps.length} {maps.length === 1 ? "map" : "maps"}
								</span>
							</p>
						</div>
						<div className="flex items-center gap-1.5">
							<Button
								variant="outline"
								size="sm"
								className="gap-1.5"
								onClick={() => setEditOpen(true)}
							>
								<PenIcon className="size-3.5" />
								Edit
							</Button>
							<Button
								variant="outline"
								size="icon"
								className="size-8 text-muted-foreground hover:text-destructive"
								aria-label="Delete list"
								onClick={() => setDeleteOpen(true)}
							>
								<TrashBinMinimalisticIcon className="size-4" />
							</Button>
						</div>
					</div>

					{maps.length === 0 ? (
						<Empty className="mt-10">
							<EmptyHeader>
								<EmptyMedia variant="icon">
									<MusicNotesIcon />
								</EmptyMedia>
								<EmptyTitle>No maps in this list</EmptyTitle>
								<EmptyDescription>
									Add maps from the{" "}
									<Link to="/maps" className="text-primary underline">
										maps browser
									</Link>{" "}
									using the list button on any map.
								</EmptyDescription>
							</EmptyHeader>
						</Empty>
					) : (
						<div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
							{maps.map((map) => (
								<ListMapCard key={map.id} listId={id} map={map} />
							))}
						</div>
					)}
				</>
			)}

			{list && (
				<EditListDialog
					listId={id}
					initialName={list.name}
					initialDescription={list.description ?? ""}
					open={editOpen}
					onOpenChange={setEditOpen}
				/>
			)}

			<AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
				<AlertDialogContent>
					<AlertDialogHeader>
						<AlertDialogTitle>Delete this list?</AlertDialogTitle>
						<AlertDialogDescription>
							"{list?.name}" will be removed. The maps themselves aren't
							deleted.
						</AlertDialogDescription>
					</AlertDialogHeader>
					<AlertDialogFooter>
						<AlertDialogCancel>Cancel</AlertDialogCancel>
						<AlertDialogAction
							className="bg-destructive text-white hover:bg-destructive/90"
							onClick={() => deleteMutation.mutate({ listId: id })}
						>
							Delete
						</AlertDialogAction>
					</AlertDialogFooter>
				</AlertDialogContent>
			</AlertDialog>
		</AppShell>
	);
}

/** A map card with a remove-from-list button overlaid. */
function ListMapCard({ listId, map }: { listId: string; map: MapListItemDto }) {
	const queryClient = useQueryClient();
	const removeMutation = useDeleteApiListsListIdMapsMapId({
		mutation: {
			onSuccess: async (res) => {
				if (res.status !== 204) {
					toast.error("Couldn't remove the map.");
					return;
				}
				await Promise.all([
					queryClient.invalidateQueries({
						queryKey: getGetApiListsListIdQueryKey(listId),
					}),
					queryClient.invalidateQueries({ queryKey: getGetApiListsQueryKey() }),
				]);
			},
			onError: () => toast.error("Couldn't remove the map."),
		},
	});

	return (
		<MapCard
			map={map}
			action={
				<button
					type="button"
					aria-label="Remove from list"
					disabled={removeMutation.isPending}
					onClick={(e) => {
						// The card is a link — don't navigate when removing.
						e.preventDefault();
						e.stopPropagation();
						removeMutation.mutate({ listId, mapId: map.id });
					}}
					className="flex size-6 items-center justify-center rounded-full bg-background/70 text-muted-foreground backdrop-blur-sm transition-colors hover:bg-background/90 hover:text-destructive disabled:opacity-60"
				>
					<CloseCircleIcon className="size-4" />
				</button>
			}
		/>
	);
}

function EditListDialog({
	listId,
	initialName,
	initialDescription,
	open,
	onOpenChange,
}: {
	listId: string;
	initialName: string;
	initialDescription: string;
	open: boolean;
	onOpenChange: (open: boolean) => void;
}) {
	const queryClient = useQueryClient();
	const [name, setName] = useState(initialName);
	const [description, setDescription] = useState(initialDescription);

	// Re-seed the fields whenever the dialog opens on the current list values.
	const reseed = () => {
		setName(initialName);
		setDescription(initialDescription);
	};

	const patchMutation = usePatchApiListsListId({
		mutation: {
			onSuccess: async (res) => {
				if (res.status !== 204) {
					toast.error("Couldn't save changes.");
					return;
				}
				await Promise.all([
					queryClient.invalidateQueries({
						queryKey: getGetApiListsListIdQueryKey(listId),
					}),
					queryClient.invalidateQueries({ queryKey: getGetApiListsQueryKey() }),
				]);
				toast.success("List updated.");
				onOpenChange(false);
			},
			onError: () => toast.error("Couldn't save changes."),
		},
	});

	const trimmed = name.trim();
	const canSubmit = trimmed.length > 0 && !patchMutation.isPending;

	return (
		<Dialog
			open={open}
			onOpenChange={(next) => {
				if (next) reseed();
				onOpenChange(next);
			}}
		>
			<DialogContent>
				<DialogHeader>
					<DialogTitle>Edit list</DialogTitle>
					<DialogDescription>
						Rename or re-describe this list.
					</DialogDescription>
				</DialogHeader>
				<FieldGroup>
					<Field>
						<FieldLabel htmlFor="edit-list-name">Name</FieldLabel>
						<Input
							id="edit-list-name"
							value={name}
							maxLength={64}
							onChange={(e) => setName(e.target.value)}
						/>
					</Field>
					<Field>
						<FieldLabel htmlFor="edit-list-description">
							Description{" "}
							<span className="text-muted-foreground">(optional)</span>
						</FieldLabel>
						<Textarea
							id="edit-list-description"
							value={description}
							maxLength={512}
							rows={3}
							onChange={(e) => setDescription(e.target.value)}
						/>
					</Field>
				</FieldGroup>
				<DialogFooter>
					<Button
						variant="ghost"
						onClick={() => onOpenChange(false)}
						disabled={patchMutation.isPending}
					>
						Cancel
					</Button>
					<Button
						disabled={!canSubmit}
						onClick={() =>
							patchMutation.mutate({
								listId,
								data: {
									name: trimmed,
									description: description.trim() || null,
								},
							})
						}
					>
						Save
					</Button>
				</DialogFooter>
			</DialogContent>
		</Dialog>
	);
}

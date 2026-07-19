import {
	Popover,
	PopoverContent,
	PopoverTrigger,
} from "@shiron/ui/components/ui/popover";
import { cn } from "@shiron/ui/lib/utils";
import {
	AddCircleIcon,
	CheckCircleIcon,
	PlaylistIcon,
} from "@solar-icons/react/dynamic";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import {
	getGetApiListsListIdQueryKey,
	getGetApiListsQueryKey,
	useDeleteApiListsListIdMapsMapId,
	useGetApiLists,
	usePutApiListsListIdMapsMapId,
} from "@/api/lists/lists";
import { CreateListDialog } from "@/components/lists/CreateListDialog";

type AddToListMenuProps = {
	mapId: string;
	/** Compact overlay style used on top of a map cover. */
	overlay?: boolean;
	className?: string;
};

/**
 * "Add to list" picker for a map. Shows the user's lists with a check for the ones
 * that already contain the map; toggling adds/removes the map. Includes a shortcut
 * to create a new list and drop the map straight into it.
 */
export function AddToListMenu({
	mapId,
	overlay,
	className,
}: AddToListMenuProps) {
	const queryClient = useQueryClient();
	const [open, setOpen] = useState(false);
	const [createOpen, setCreateOpen] = useState(false);

	const listsQuery = useGetApiLists({ mapId }, { query: { enabled: open } });
	const lists = listsQuery.data?.status === 200 ? listsQuery.data.data : [];

	const listsKey = getGetApiListsQueryKey({ mapId });

	const invalidate = (listId: string) =>
		Promise.all([
			queryClient.invalidateQueries({ queryKey: getGetApiListsQueryKey() }),
			queryClient.invalidateQueries({
				queryKey: getGetApiListsListIdQueryKey(listId),
			}),
		]);

	// Flip the map's membership in the popover's own list cache so the check and
	// count update the instant you click, before the request resolves. Returns
	// the previous cache so a failed request can roll back.
	const applyOptimistic = async (listId: string, contains: boolean) => {
		await queryClient.cancelQueries({ queryKey: listsKey });
		const previous = queryClient.getQueryData(listsKey);
		queryClient.setQueryData<typeof listsQuery.data>(listsKey, (old) => {
			if (old?.status !== 200) return old;
			return {
				...old,
				data: old.data.map((l) =>
					l.id === listId
						? {
								...l,
								containsMap: contains,
								mapCount: Number(l.mapCount) + (contains ? 1 : -1),
							}
						: l,
				),
			};
		});
		return previous;
	};

	const addMutation = usePutApiListsListIdMapsMapId({
		mutation: {
			onMutate: async ({ listId }) => ({
				previous: await applyOptimistic(listId, true),
			}),
			onError: (_err, _vars, ctx) => {
				if (ctx?.previous) queryClient.setQueryData(listsKey, ctx.previous);
				toast.error("Couldn't add the map.");
			},
			onSettled: (_res, _err, vars) => invalidate(vars.listId),
		},
	});
	const removeMutation = useDeleteApiListsListIdMapsMapId({
		mutation: {
			onMutate: async ({ listId }) => ({
				previous: await applyOptimistic(listId, false),
			}),
			onError: (_err, _vars, ctx) => {
				if (ctx?.previous) queryClient.setQueryData(listsKey, ctx.previous);
				toast.error("Couldn't remove the map.");
			},
			onSettled: (_res, _err, vars) => invalidate(vars.listId),
		},
	});

	const busy = addMutation.isPending || removeMutation.isPending;

	const toggle = (listId: string, contains: boolean) => {
		if (contains) {
			removeMutation.mutate({ listId, mapId });
		} else {
			addMutation.mutate({ listId, mapId });
		}
	};

	// The trigger often lives inside a map-card <Link>. We preventDefault +
	// stopPropagation to keep the card from navigating, but that also suppresses
	// Radix's built-in open-on-click (it skips its handler once defaultPrevented),
	// so we drive the open state ourselves.
	const onTriggerClick = (e: React.MouseEvent) => {
		e.preventDefault();
		e.stopPropagation();
		setOpen((prev) => !prev);
	};

	return (
		<>
			<Popover open={open} onOpenChange={setOpen}>
				<PopoverTrigger asChild>
					<button
						type="button"
						aria-label="Add to list"
						onClick={onTriggerClick}
						className={cn(
							"flex items-center justify-center rounded-full text-muted-foreground transition-colors hover:text-foreground",
							overlay
								? "size-6 bg-background/70 backdrop-blur-sm hover:bg-background/90"
								: "size-8 hover:bg-foreground/10",
							className,
						)}
					>
						<PlaylistIcon className="size-3.5" />
					</button>
				</PopoverTrigger>
				<PopoverContent align="start" className="w-60 p-1.5">
					<p className="px-2 py-1.5 text-xs font-medium text-muted-foreground">
						Add to list
					</p>

					<div className="max-h-64 overflow-y-auto">
						{listsQuery.isLoading && (
							<p className="px-2 py-2 text-xs text-muted-foreground">
								Loading…
							</p>
						)}
						{!listsQuery.isLoading && lists.length === 0 && (
							<p className="px-2 py-2 text-xs text-muted-foreground">
								No lists yet.
							</p>
						)}
						{lists.map((list) => {
							const contains = list.containsMap === true;
							return (
								<button
									key={list.id}
									type="button"
									disabled={busy}
									onClick={() => toggle(list.id, contains)}
									className="flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-sm transition-colors hover:bg-foreground/5 disabled:opacity-60"
								>
									<span
										className={cn(
											"flex size-4 shrink-0 items-center justify-center rounded-full border",
											contains
												? "border-primary bg-primary text-primary-foreground"
												: "border-border",
										)}
									>
										{contains && (
											<CheckCircleIcon className="size-3" weight="Bold" />
										)}
									</span>
									<span className="min-w-0 flex-1 truncate">{list.name}</span>
									<span className="shrink-0 text-xs tabular-nums text-muted-foreground">
										{Number(list.mapCount)}
									</span>
								</button>
							);
						})}
					</div>

					<div className="mt-1 border-t border-border pt-1">
						<button
							type="button"
							onClick={() => {
								setOpen(false);
								setCreateOpen(true);
							}}
							className="flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-sm text-primary transition-colors hover:bg-foreground/5"
						>
							<AddCircleIcon className="size-4" />
							New list…
						</button>
					</div>
				</PopoverContent>
			</Popover>

			<CreateListDialog
				open={createOpen}
				onOpenChange={setCreateOpen}
				onCreated={(listId) => addMutation.mutate({ listId, mapId })}
			/>
		</>
	);
}

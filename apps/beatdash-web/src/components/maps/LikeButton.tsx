import { cn } from "@shiron/ui/lib/utils";
import { HeartIcon } from "@solar-icons/react/dynamic";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import {
	getGetApiMapsMapIdQueryKey,
	getGetApiMapsQueryKey,
	useDeleteApiMapsMapIdLike,
	usePutApiMapsMapIdLike,
} from "@/api/maps/maps";

type LikeButtonProps = {
	mapId: string;
	isLiked: boolean;
	likeCount?: number;
	/** Show the total like count next to the heart. */
	showCount?: boolean;
	/** Compact overlay style used on top of a map cover. */
	overlay?: boolean;
	className?: string;
};

/**
 * Heart toggle for liking a whole map. Optimistically flips its own state, then
 * invalidates the maps list + this map's detail so every surface stays in sync.
 * Safe to place inside a clickable card — it swallows the click so the card's
 * navigation doesn't fire.
 */
export function LikeButton({
	mapId,
	isLiked,
	likeCount = 0,
	showCount = false,
	overlay = false,
	className,
}: LikeButtonProps) {
	const queryClient = useQueryClient();

	// Optimistic local state, re-synced whenever the server value changes.
	const [liked, setLiked] = useState(isLiked);
	const [count, setCount] = useState(likeCount);
	useEffect(() => setLiked(isLiked), [isLiked]);
	useEffect(() => setCount(likeCount), [likeCount]);

	const invalidate = async () => {
		await Promise.all([
			queryClient.invalidateQueries({ queryKey: getGetApiMapsQueryKey() }),
			queryClient.invalidateQueries({
				queryKey: getGetApiMapsMapIdQueryKey(mapId),
			}),
		]);
	};

	const likeMutation = usePutApiMapsMapIdLike({
		mutation: {
			onSuccess: invalidate,
			onError: () => {
				setLiked(false);
				setCount((c) => Math.max(0, c - 1));
				toast.error("Couldn't like this map.");
			},
		},
	});
	const unlikeMutation = useDeleteApiMapsMapIdLike({
		mutation: {
			onSuccess: invalidate,
			onError: () => {
				setLiked(true);
				setCount((c) => c + 1);
				toast.error("Couldn't remove the like.");
			},
		},
	});

	const busy = likeMutation.isPending || unlikeMutation.isPending;

	const toggle = (e: React.MouseEvent) => {
		// Stop the surrounding card link from navigating.
		e.preventDefault();
		e.stopPropagation();
		if (busy) return;

		if (liked) {
			setLiked(false);
			setCount((c) => Math.max(0, c - 1));
			unlikeMutation.mutate({ mapId });
		} else {
			setLiked(true);
			setCount((c) => c + 1);
			likeMutation.mutate({ mapId });
		}
	};

	return (
		<button
			type="button"
			onClick={toggle}
			aria-pressed={liked}
			aria-label={liked ? "Unlike map" : "Like map"}
			disabled={busy}
			className={cn(
				"flex items-center gap-1 rounded-full text-xs font-medium transition-colors disabled:opacity-60",
				overlay
					? "bg-background/70 px-1.5 py-0.5 backdrop-blur-sm hover:bg-background/90"
					: "px-2 py-1 hover:bg-foreground/10",
				liked ? "text-rose-500" : "text-muted-foreground hover:text-foreground",
				className,
			)}
		>
			<HeartIcon className="size-3.5" weight={liked ? "Bold" : "Linear"} />
			{showCount && <span className="tabular-nums">{count}</span>}
		</button>
	);
}

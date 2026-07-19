import { Button } from "@shiron/ui/components/ui/button";
import {
	Empty,
	EmptyContent,
	EmptyDescription,
	EmptyHeader,
	EmptyMedia,
	EmptyTitle,
} from "@shiron/ui/components/ui/empty";
import { cn } from "@shiron/ui/lib/utils";
import { DangerTriangleIcon } from "@solar-icons/react/dynamic";

/**
 * Generic "couldn't load" surface for a failed data query. Intentionally vague —
 * it never renders the underlying error so backend/internal state never leaks —
 * with an optional retry that re-runs the query.
 */
export function ErrorState({
	title = "Couldn't load this",
	description = "Something went wrong while loading. Please try again.",
	onRetry,
	className,
}: {
	title?: string;
	description?: string;
	onRetry?: () => void;
	className?: string;
}) {
	return (
		<Empty className={cn("mt-10", className)}>
			<EmptyHeader>
				<EmptyMedia variant="icon">
					<DangerTriangleIcon />
				</EmptyMedia>
				<EmptyTitle>{title}</EmptyTitle>
				<EmptyDescription>{description}</EmptyDescription>
			</EmptyHeader>
			{onRetry && (
				<EmptyContent>
					<Button variant="outline" onClick={onRetry}>
						Retry
					</Button>
				</EmptyContent>
			)}
		</Empty>
	);
}

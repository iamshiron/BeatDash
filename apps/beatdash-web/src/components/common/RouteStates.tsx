import { Button } from "@shiron/ui/components/ui/button";
import {
	Empty,
	EmptyContent,
	EmptyDescription,
	EmptyHeader,
	EmptyMedia,
	EmptyTitle,
} from "@shiron/ui/components/ui/empty";
import { Skeleton } from "@shiron/ui/components/ui/skeleton";
import {
	DangerTriangleIcon,
	QuestionCircleIcon,
} from "@solar-icons/react/dynamic";
import { Link } from "@tanstack/react-router";
import { AppShell } from "@/components/layout/AppShell";

/**
 * Router-level fallback shown when a route throws while loading or rendering.
 * Deliberately generic — it never surfaces the underlying error text so backend
 * details don't leak — and offers a retry that resets the error boundary.
 */
export function RouteError({ reset }: { error: Error; reset: () => void }) {
	return (
		<AppShell>
			<Empty className="mt-10">
				<EmptyHeader>
					<EmptyMedia variant="icon">
						<DangerTriangleIcon />
					</EmptyMedia>
					<EmptyTitle>Something went wrong</EmptyTitle>
					<EmptyDescription>
						This page ran into an unexpected error. Please try again.
					</EmptyDescription>
				</EmptyHeader>
				<EmptyContent>
					<div className="flex flex-wrap justify-center gap-2">
						<Button onClick={reset}>Try again</Button>
						<Button variant="outline" asChild>
							<Link to="/">Back to dashboard</Link>
						</Button>
					</div>
				</EmptyContent>
			</Empty>
		</AppShell>
	);
}

/** Router-level pending fallback shown while a route is still loading. */
export function RoutePending() {
	return (
		<AppShell>
			<div className="flex flex-col gap-4">
				<Skeleton className="h-20 rounded-xl" />
				<Skeleton className="h-40 rounded-xl" />
				<Skeleton className="h-40 rounded-xl" />
			</div>
		</AppShell>
	);
}

/** Router-level 404 shown for unmatched routes. */
export function NotFound() {
	return (
		<AppShell>
			<Empty className="mt-10">
				<EmptyHeader>
					<EmptyMedia variant="icon">
						<QuestionCircleIcon />
					</EmptyMedia>
					<EmptyTitle>Page not found</EmptyTitle>
					<EmptyDescription>
						The page you're looking for doesn't exist or has moved.
					</EmptyDescription>
				</EmptyHeader>
				<EmptyContent>
					<Button asChild>
						<Link to="/">Back to dashboard</Link>
					</Button>
				</EmptyContent>
			</Empty>
		</AppShell>
	);
}

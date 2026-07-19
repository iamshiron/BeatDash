import { useRouterState } from "@tanstack/react-router";

/**
 * Fades and slides its content in on navigation. Keyed by pathname, so it
 * re-animates only when the page itself changes — not on search-param updates
 * like pagination or filtering within the same route.
 */
export function PageTransition({ children }: { children: React.ReactNode }) {
	const pathname = useRouterState({ select: (s) => s.location.pathname });
	return (
		<div key={pathname} className="page-transition">
			{children}
		</div>
	);
}

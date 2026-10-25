import {
	createFileRoute,
	Link,
	Outlet,
	redirect,
} from "@tanstack/react-router";
import { PageTransition } from "@/components/common/PageTransition";

export const Route = createFileRoute("/auth")({
	beforeLoad: ({ context }) => {
		// The desktop app has no accounts, so there is nothing to sign in to.
		if (context.auth.isAuthenticated || context.auth.isDesktop) {
			throw redirect({ to: "/", replace: true });
		}
	},
	component: AuthLayout,
});

function AuthLayout() {
	return (
		<main className="flex min-h-screen flex-col items-center justify-center gap-8 px-4 py-12">
			<Link
				to="/"
				className="font-heading text-lg font-semibold tracking-tight"
			>
				<span className="text-primary">BeatDash</span>
			</Link>
			<div className="w-full max-w-sm">
				<PageTransition>
					<Outlet />
				</PageTransition>
			</div>
		</main>
	);
}

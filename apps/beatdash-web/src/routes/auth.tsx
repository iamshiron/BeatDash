import {
	createFileRoute,
	Link,
	Outlet,
	redirect,
} from "@tanstack/react-router";
import { getMe } from "@/api/auth/auth";

export const Route = createFileRoute("/auth")({
	beforeLoad: async ({ context }) => {
		if (context.auth.isAuthenticated) {
			throw redirect({ to: "/", replace: true });
		}
		let authenticated = false;
		try {
			const response = await getMe();
			authenticated = response.status === 200;
		} catch {
			authenticated = false;
		}
		if (authenticated) {
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
				<Outlet />
			</div>
		</main>
	);
}

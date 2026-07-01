import { createFileRoute, Link, Outlet } from "@tanstack/react-router";

export const Route = createFileRoute("/auth")({
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

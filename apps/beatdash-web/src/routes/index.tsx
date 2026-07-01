import { createFileRoute, Link } from "@tanstack/react-router";
import { AppShell } from "@/components/layout/AppShell";
import { Button } from "@shiron/ui/components/ui/button";

export const Route = createFileRoute("/")({
	component: LandingPage,
});

function LandingPage() {
	return (
		<AppShell>
			<div className="flex flex-col items-center justify-center gap-8 py-20 text-center">
				<div className="space-y-4">
					<h1 className="font-heading text-5xl font-bold tracking-tight">
						<span className="text-primary">BeatDash</span>
					</h1>
					<p className="mx-auto max-w-md text-muted-foreground">
						Track your Beat Saber stats in one place and get detailed insight
						into your play habits.
					</p>
				</div>
				<div className="flex gap-3">
					<Button asChild size="lg">
						<Link to="/auth/register">Get Started</Link>
					</Button>
					<Button asChild size="lg" variant="outline">
						<Link to="/auth/login">Sign In</Link>
					</Button>
				</div>
			</div>
		</AppShell>
	);
}

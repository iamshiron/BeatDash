import { Link } from "@tanstack/react-router";
import { ModeToggle } from "@/components/layout/ModeToggle";
import { Button } from "@shiron/ui/components/ui/button";

export function AppShell({ children }: { children: React.ReactNode }) {
	return (
		<div className="min-h-screen bg-background">
			<header className="sticky top-4 z-50 mx-auto max-w-5xl rounded-2xl left-0 right-0 px-4 bg-background/95 backdrop-blur-md shadow-lg border border-border">
				<div className="flex h-14 items-center justify-between px-6">
					<Link to="/" className="flex items-center gap-2">
						<span className="text-xl font-bold text-primary">BeatDash</span>
					</Link>
					<div className="flex gap-2">
						<nav className="flex items-center gap-1">
							<Button asChild variant="ghost">
								<Link to="/dashboard">Dashboard</Link>
							</Button>
						</nav>
						<nav className="flex items-center gap-1">
							<Button asChild variant="ghost">
								<Link to="/sessions">Sessions</Link>
							</Button>
						</nav>
						<nav className="flex items-center gap-1">
							<Button asChild variant="ghost">
								<Link to="/maps">Maps</Link>
							</Button>
						</nav>
					</div>
					<div className="flex items-center gap-2">
						<ModeToggle />
					</div>
				</div>
			</header>
			<main className="container mx-auto px-4 py-6">{children}</main>
		</div>
	);
}

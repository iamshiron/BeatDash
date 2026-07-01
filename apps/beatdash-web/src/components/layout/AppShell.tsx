import { Link } from "@tanstack/react-router";
import { ModeToggle } from "@/components/layout/ModeToggle";

export function AppShell({ children }: { children: React.ReactNode }) {
	return (
		<div className="relative min-h-screen bg-background">
			<header className="sticky top-4 z-50 mx-auto w-full max-w-3xl px-4">
				<div className="glass flex h-12 items-center justify-between rounded-full border border-border pl-5 pr-2 shadow-sm">
					<Link to="/" className="flex items-center gap-2">
						<span className="flex size-6 items-center justify-center rounded-md bg-primary font-heading text-xs font-bold text-primary-foreground">
							B
						</span>
						<span className="font-heading text-sm font-semibold tracking-tight">
							BeatDash
						</span>
					</Link>
					<div className="flex items-center gap-1">
						<ModeToggle />
					</div>
				</div>
			</header>
			<main className="mx-auto w-full max-w-5xl px-4 py-10">{children}</main>
		</div>
	);
}

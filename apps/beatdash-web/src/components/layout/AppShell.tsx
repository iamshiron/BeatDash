import { Link } from "@tanstack/react-router";
import { Button } from "@shiron/ui/components/ui/button";
import { ModeToggle } from "@/components/layout/ModeToggle";
import { UserMenu } from "@/components/layout/UserMenu";
import { useAuth } from "@/contexts/auth";

export function AppShell({ children }: { children: React.ReactNode }) {
	const { isAuthenticated } = useAuth();
	return (
		<div className="relative min-h-screen bg-background">
			<header className="sticky top-4 z-50 mx-auto w-full max-w-5xl px-4">
				<div className="glass grid h-12 grid-cols-[1fr_auto_1fr] items-center rounded-full border border-border pl-5 pr-2 shadow-sm">
					<div className="justify-self-start">
						<Link to="/" className="flex items-center gap-2">
							<span className="flex size-6 items-center justify-center rounded-md bg-primary font-heading text-xs font-bold text-primary-foreground">
								B
							</span>
							<span className="font-heading text-sm font-semibold tracking-tight">
								BeatDash
							</span>
						</Link>
					</div>
					<nav className="justify-self-center">
						{isAuthenticated && (
							<Button variant="ghost" size="sm" asChild>
								<Link to="/devices">Devices</Link>
							</Button>
						)}
					</nav>
					<div className="flex items-center gap-1 justify-self-end">
						<ModeToggle />
						{isAuthenticated ? (
							<UserMenu />
						) : (
							<Button variant="ghost" size="sm" asChild>
								<Link to="/auth/login">Sign In</Link>
							</Button>
						)}
					</div>
				</div>
			</header>
			<main className="mx-auto w-full max-w-3xl px-4 py-10">{children}</main>
		</div>
	);
}

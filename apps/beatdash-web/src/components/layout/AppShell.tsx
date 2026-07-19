import { Button } from "@shiron/ui/components/ui/button";
import { cn } from "@shiron/ui/lib/utils";
import { Link } from "@tanstack/react-router";
import { PageTransition } from "@/components/common/PageTransition";
import { MobileNav } from "@/components/layout/MobileNav";
import { ModeToggle } from "@/components/layout/ModeToggle";
import { UserMenu } from "@/components/layout/UserMenu";
import { useAuth } from "@/contexts/auth";
import { usePlayer } from "@/contexts/player";

const NAV_ITEMS = [
	{ to: "/", label: "Dashboard", exact: true },
	{ to: "/devices", label: "Devices", exact: false },
	{ to: "/maps", label: "Maps", exact: false },
	{ to: "/plays", label: "Plays", exact: false },
	{ to: "/lists", label: "Lists", exact: false },
	{ to: "/analysis", label: "Analysis", exact: false },
	{ to: "/live", label: "Live", exact: false },
] as const;

export function AppShell({
	children,
	wide = false,
}: {
	children: React.ReactNode;
	wide?: boolean;
}) {
	const maxWidth = wide ? "max-w-6xl" : "max-w-5xl";
	const { isAuthenticated } = useAuth();
	const { track } = usePlayer();
	return (
		<div className="relative min-h-screen">
			<header className={`sticky top-4 z-50 mx-auto w-full ${maxWidth} px-4`}>
				<div className="glass flex h-12 items-center justify-between rounded-full border border-border pl-5 pr-2 shadow-sm md:grid md:grid-cols-[1fr_auto_1fr]">
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
					<nav className="hidden justify-self-center md:block">
						{isAuthenticated && (
							<div className="flex items-center gap-1">
								{NAV_ITEMS.map((item) => (
									<Button key={item.to} variant="ghost" size="sm" asChild>
										<Link
											to={item.to}
											activeOptions={{ exact: item.exact }}
											className="text-muted-foreground [&.active]:bg-accent [&.active]:text-accent-foreground"
										>
											{item.label}
										</Link>
									</Button>
								))}
							</div>
						)}
					</nav>
					<div className="flex items-center gap-1 justify-self-end">
						<ModeToggle />
						{isAuthenticated ? (
							<>
								<UserMenu />
								<MobileNav className="md:hidden" />
							</>
						) : (
							<Button variant="ghost" size="sm" asChild>
								<Link to="/auth/login">Sign In</Link>
							</Button>
						)}
					</div>
				</div>
			</header>
			<main
				className={cn(
					"mx-auto w-full px-4 py-10",
					wide ? "max-w-6xl" : "max-w-3xl",
					// Clear the fixed bottom player so it never covers page content.
					track && "pb-32",
				)}
			>
				<PageTransition>{children}</PageTransition>
			</main>
		</div>
	);
}

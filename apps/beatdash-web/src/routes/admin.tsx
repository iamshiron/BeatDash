import { TestTube, SpeedometerMiddle } from "@solar-icons/react";
import { Separator } from "@shiron/ui/components/ui/separator";
import {
	Sidebar,
	SidebarContent,
	SidebarGroup,
	SidebarGroupContent,
	SidebarGroupLabel,
	SidebarHeader,
	SidebarInset,
	SidebarMenu,
	SidebarMenuButton,
	SidebarMenuItem,
	SidebarProvider,
	SidebarRail,
	SidebarTrigger,
} from "@shiron/ui/components/ui/sidebar";
import {
	createFileRoute,
	Link,
	Outlet,
	redirect,
	useRouterState,
} from "@tanstack/react-router";

export const Route = createFileRoute("/admin")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
		if (!context.auth.isAdmin) {
			throw redirect({ to: "/", replace: true });
		}
	},
	component: AdminLayout,
});

interface AdminNavItem {
	label: string;
	to: string;
	icon: typeof SpeedometerMiddle;
	exact?: boolean;
}

/** Admin sidebar navigation. Add admin-only routes here. */
const NAV_ITEMS: AdminNavItem[] = [
	{ label: "Overview", to: "/admin", icon: SpeedometerMiddle, exact: true },
	{ label: "Scoring Lab", to: "/admin/scoring", icon: TestTube },
];

function AdminLayout() {
	const pathname = useRouterState({ select: (s) => s.location.pathname });

	return (
		<SidebarProvider>
			<Sidebar>
				<SidebarHeader>
					<Link to="/" className="flex items-center gap-2 px-2 py-1.5">
						<span className="flex size-6 items-center justify-center rounded-md bg-primary font-heading text-xs font-bold text-primary-foreground">
							B
						</span>
						<span className="font-heading text-sm font-semibold tracking-tight">
							BeatDash Admin
						</span>
					</Link>
				</SidebarHeader>
				<SidebarContent>
					<SidebarGroup>
						<SidebarGroupLabel>Administration</SidebarGroupLabel>
						<SidebarGroupContent>
							<SidebarMenu>
								{NAV_ITEMS.map((item) => {
									const isActive = item.exact
										? pathname === item.to
										: pathname.startsWith(item.to);
									return (
										<SidebarMenuItem key={item.to}>
											<SidebarMenuButton asChild isActive={isActive}>
												<Link to={item.to}>
													<item.icon />
													<span>{item.label}</span>
												</Link>
											</SidebarMenuButton>
										</SidebarMenuItem>
									);
								})}
							</SidebarMenu>
						</SidebarGroupContent>
					</SidebarGroup>
				</SidebarContent>
				<SidebarRail />
			</Sidebar>
			<SidebarInset>
				<header className="flex h-12 shrink-0 items-center gap-2 border-b border-border px-4">
					<SidebarTrigger className="-ml-1" />
					<Separator orientation="vertical" className="mr-2 h-4" />
					<span className="font-heading text-sm font-semibold tracking-tight">
						Admin
					</span>
				</header>
				<main className="flex-1 p-6">
					<Outlet />
				</main>
			</SidebarInset>
		</SidebarProvider>
	);
}

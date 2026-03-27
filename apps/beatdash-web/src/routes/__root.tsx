import { createRootRoute, Outlet } from "@tanstack/react-router";
import { AppShell } from "@/components/layout/AppShell";
import { Toaster } from "@shiron/ui/components/ui/sonner";
import { TooltipProvider } from "@shiron/ui/components/ui/tooltip";

export const Route = createRootRoute({
	component: () => (
		<TooltipProvider>
			<AppShell>
				<Outlet />
			</AppShell>
			<Toaster />
		</TooltipProvider>
	),
});

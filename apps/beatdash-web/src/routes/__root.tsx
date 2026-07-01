import { createRootRoute, Outlet } from "@tanstack/react-router";
import { Toaster } from "@shiron/ui/components/ui/sonner";
import { TooltipProvider } from "@shiron/ui/components/ui/tooltip";

export const Route = createRootRoute({
	component: RootComponent,
});

function RootComponent() {
	return (
		<TooltipProvider>
			<Outlet />
			<Toaster />
		</TooltipProvider>
	);
}

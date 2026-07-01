import type { QueryClient } from "@tanstack/react-query";
import { createRootRouteWithContext, Outlet } from "@tanstack/react-router";
import type { AuthValue } from "@/contexts/auth";
import { Toaster } from "@shiron/ui/components/ui/sonner";
import { TooltipProvider } from "@shiron/ui/components/ui/tooltip";

export interface RouterContext {
	auth: AuthValue;
	queryClient: QueryClient;
}

export const Route = createRootRouteWithContext<RouterContext>()({
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

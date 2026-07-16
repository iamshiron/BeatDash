import { Background } from "@shiron/ui/components/ui/background";
import { Toaster } from "@shiron/ui/components/ui/sonner";
import { TooltipProvider } from "@shiron/ui/components/ui/tooltip";
import type { QueryClient } from "@tanstack/react-query";
import { createRootRouteWithContext, Outlet } from "@tanstack/react-router";
import { BottomPlayer } from "@/components/player/BottomPlayer";
import type { AuthValue } from "@/contexts/auth";

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
			<Background variant="atmosphere" />
			<Outlet />
			<BottomPlayer />
			<Toaster />
		</TooltipProvider>
	);
}

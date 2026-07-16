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
			<Background>
				{/* Slight frosted wash so the ambient blobs read softer behind content. */}
				<div className="glass absolute inset-0 border-0 opacity-50" />
			</Background>
			<Outlet />
			<BottomPlayer />
			<Toaster />
		</TooltipProvider>
	);
}

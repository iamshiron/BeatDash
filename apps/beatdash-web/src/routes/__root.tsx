import {
	Background,
	BackgroundWash,
} from "@shiron/ui/components/ui/background";
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
				{/* Slight wash layered over the blobs so they read softer behind content. */}
				<BackgroundWash />
			</Background>
			<Outlet />
			<BottomPlayer />
			<Toaster />
		</TooltipProvider>
	);
}

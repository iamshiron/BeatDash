import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { createRouter, RouterProvider } from "@tanstack/react-router";
import { ThemeProvider } from "next-themes";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AuthProvider, useAuth } from "@/contexts/auth";
import { RealtimeProvider } from "@/realtime";
import type { RouterContext } from "@/routes/__root";
import "@/styles/globals.css";
import { routeTree } from "./routeTree.gen";

const queryClient = new QueryClient({
	defaultOptions: {
		queries: {
			staleTime: 1000 * 60 * 5,
			refetchOnWindowFocus: false,
		},
	},
});

const router = createRouter({
	routeTree,
	context: {
		auth: {
			user: undefined,
			isLoading: true,
			isAuthenticated: false,
			isAdmin: false,
		},
		queryClient,
	} satisfies RouterContext,
});

declare module "@tanstack/react-router" {
	interface Register {
		router: typeof router;
	}
}

function App() {
	const auth = useAuth();
	if (auth.isLoading) {
		return null;
	}
	return <RouterProvider router={router} context={{ auth, queryClient }} />;
}

// biome-ignore lint/style/noNonNullAssertion: standard React entry point
createRoot(document.getElementById("root")!).render(
	<StrictMode>
		<ThemeProvider
			attribute="class"
			defaultTheme="dark"
			enableSystem
			disableTransitionOnChange
		>
			<QueryClientProvider client={queryClient}>
				<AuthProvider>
					<RealtimeProvider>
						<App />
					</RealtimeProvider>
					<ReactQueryDevtools buttonPosition="bottom-right" />
				</AuthProvider>
			</QueryClientProvider>
		</ThemeProvider>
	</StrictMode>,
);

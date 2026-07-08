import { createFileRoute } from "@tanstack/react-router";

export const Route = createFileRoute("/admin/")({
	component: AdminOverviewPage,
});

function AdminOverviewPage() {
	return (
		<div>
			<h1 className="font-heading text-lg font-semibold tracking-tight">
				Overview
			</h1>
			<p className="mt-1 text-sm text-muted-foreground">
				Admin dashboard. Content coming soon.
			</p>
		</div>
	);
}

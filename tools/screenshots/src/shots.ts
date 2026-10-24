import type { PagedResultOfPlaySessionListItemDto } from "../../../apps/beatdash-web/src/api/model/index.ts";
import type { Shot } from "./shot.ts";

/**
 * Every screenshot the tool can produce. Add an entry to cover a new area; it can then be
 * regenerated on its own with `pnpm screenshots <name>`.
 */
export const shots: readonly Shot[] = [
	{
		name: "dashboard",
		output: "hero-dashboard.png",
		signedIn: true,
		path: "/",
	},
	{
		name: "play-detail",
		output: "feature-play-detail.png",
		signedIn: true,
		path: async (api) => {
			const plays = await api.get<PagedResultOfPlaySessionListItemDto>(
				"/api/sessions?pageSize=1",
			);
			const latest = plays.items[0];
			if (!latest) throw new Error("The fixture has no finished plays.");
			return `/plays/${latest.id}`;
		},
	},
	{
		name: "analysis",
		output: "feature-analysis.png",
		signedIn: true,
		path: "/analysis",
	},
	{
		name: "maps",
		output: "feature-maps.png",
		signedIn: true,
		path: "/maps",
	},
	{
		name: "profile",
		output: "feature-profile.png",
		signedIn: false,
		path: "/u/shiron",
	},
];

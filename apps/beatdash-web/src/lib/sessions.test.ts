import { describe, expect, it } from "vitest";
import {
	getActiveFilters,
	parseDurationMs,
	parseSessionSearch,
	toApiParams,
} from "./sessions";

describe("parseSessionSearch", () => {
	it("clamps numeric filters and rejects invalid dates and ranks", () => {
		const search = parseSessionSearch({
			page: "-2",
			amin: "-5",
			amax: "150",
			from: "2026-02-30",
			to: "2026-02-28",
			rank: "Legendary",
			fail: "false",
		});

		expect(search).toMatchObject({
			page: 1,
			amin: 0,
			amax: 100,
			to: "2026-02-28",
			fail: false,
			quit: true,
			inc: true,
		});
		expect(search.from).toBeUndefined();
		expect(search.rank).toBeUndefined();
	});
});

describe("toApiParams", () => {
	it("converts user-facing filters into API query values", () => {
		const params = toApiParams({
			page: 2,
			q: "  Camellia  ",
			diff: "ExpertPlus",
			dir: "Asc",
			auto: true,
			from: "2026-05-01",
			to: "2026-05-31",
			amin: 91,
			amax: 99,
			rank: "SS",
			fc: true,
		});

		expect(params).toMatchObject({
			Page: 2,
			PageSize: 25,
			Search: "Camellia",
			Difficulty: 4,
			SortBy: 0,
			SortDir: 0,
			IncludeAuto: true,
			From: "2026-05-01T00:00:00.000Z",
			To: "2026-05-31T23:59:59.999Z",
			MinAccuracy: 0.91,
			MaxAccuracy: 0.99,
			Rank: "SS",
			FullComboOnly: true,
		});
	});
});

describe("session display helpers", () => {
	it("groups removable filters and parses multi-day durations", () => {
		const filters = getActiveFilters({
			from: "2026-05-01",
			to: "2026-05-02",
			amin: 95,
			auto: true,
		});

		expect(filters.map((filter) => filter.id)).toEqual([
			"date",
			"accuracy",
			"auto",
		]);
		expect(parseDurationMs("1.02:03:04.500")).toBe(93_784_500);
		expect(parseDurationMs(null)).toBe(0);
	});
});

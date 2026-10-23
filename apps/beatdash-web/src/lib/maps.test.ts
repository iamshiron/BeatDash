import { describe, expect, it } from "vitest";
import {
	mapsSearchToApi,
	normalizeMapsSearch,
	parseMapRangeInput,
} from "./maps";

describe("parseMapRangeInput", () => {
	it("Validated draft values before they reach URL normalization", () => {
		expect(parseMapRangeInput("120.5", "duration").valid).toBe(false);
		expect(parseMapRangeInput("1.01", "rating").valid).toBe(false);
		expect(parseMapRangeInput("1e39", "float").valid).toBe(false);
		expect(parseMapRangeInput("0.5", "rating")).toEqual({
			valid: true,
			value: 0.5,
		});
		expect(parseMapRangeInput("120", "duration")).toEqual({
			valid: true,
			value: 120,
		});
		expect(parseMapRangeInput("", "number")).toEqual({
			valid: true,
			value: undefined,
		});
		expect(
			normalizeMapsSearch({ bpmMax: "1e39", npsMin: "1e39" }),
		).toMatchObject({ bpmMax: undefined, npsMin: undefined });
	});
});

describe("normalizeMapsSearch", () => {
	it("Accepted validated catalog filters", () => {
		const search = normalizeMapsSearch({
			p: "3",
			difficulty: "expert-plus",
			bpmMin: "120",
			bpmMax: "240",
			ranked: "yes",
			sort: "pp",
			direction: "asc",
		});

		expect(search).toMatchObject({
			p: 3,
			difficulty: "expert-plus",
			bpmMin: 120,
			bpmMax: 240,
			ranked: "yes",
			sort: "pp",
			direction: "asc",
		});
	});

	it("Dropped invalid values and inverted ranges", () => {
		const search = normalizeMapsSearch({
			p: -4,
			difficulty: "impossible",
			bpmMin: 300,
			bpmMax: 100,
			fetchStatus: "unknown",
		});

		expect(search).toMatchObject({ p: 1 });
		expect(search.difficulty).toBeUndefined();
		expect(search.bpmMin).toBeUndefined();
		expect(search.bpmMax).toBeUndefined();
		expect(search.fetchStatus).toBeUndefined();
	});

	it("Rejected coercible, out-of-bounds, and non-integer URL values", () => {
		const search = normalizeMapsSearch({
			p: "1.5",
			bpmMin: null,
			bpmMax: false,
			npsMin: "",
			npsMax: [],
			ppMin: " 12",
			ppMax: "12 maps",
			durationMin: "120.5",
			durationMax: 2_147_483_648,
			ratingMin: -0.01,
			ratingMax: 1.01,
		});

		expect(search.p).toBe(1);
		expect(search.bpmMin).toBeUndefined();
		expect(search.bpmMax).toBeUndefined();
		expect(search.npsMin).toBeUndefined();
		expect(search.npsMax).toBeUndefined();
		expect(search.ppMin).toBeUndefined();
		expect(search.ppMax).toBeUndefined();
		expect(search.durationMin).toBeUndefined();
		expect(search.durationMax).toBeUndefined();
		expect(search.ratingMin).toBeUndefined();
		expect(search.ratingMax).toBeUndefined();
		expect(normalizeMapsSearch({ p: 2_147_483_648 }).p).toBe(1);
	});

	it("Accepted API-compatible numeric strings at their bounds", () => {
		const search = normalizeMapsSearch({
			p: "2147483647",
			durationMin: "0",
			durationMax: "2147483647",
			ratingMin: "0",
			ratingMax: "1",
			npsMin: "1e1",
		});

		expect(search).toMatchObject({
			p: 2_147_483_647,
			durationMin: 0,
			durationMax: 2_147_483_647,
			ratingMin: 0,
			ratingMax: 1,
			npsMin: 10,
		});
	});
});

describe("mapsSearchToApi", () => {
	it("Mapped semantic URL tokens to API enum values", () => {
		const api = mapsSearchToApi(
			normalizeMapsSearch({
				difficulty: "expert",
				fetchStatus: "failed",
				ranked: "no",
				played: "unplayed",
				sort: "uploaded",
				direction: "asc",
			}),
		);

		expect(api).toMatchObject({
			Difficulty: 3,
			FetchStatus: 4,
			Ranked: false,
			Played: false,
			SortBy: 10,
			SortDir: 0,
		});
	});
});

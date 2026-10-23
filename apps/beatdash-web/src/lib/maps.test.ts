import { describe, expect, it } from "vitest";
import { mapsSearchToApi, normalizeMapsSearch } from "./maps";

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

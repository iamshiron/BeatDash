import type { UpdateProfileDto, UserInfoDto } from "@/api/model";

/** Coerces an orval `number | string | null` field to a number or null. */
function num(value: number | string | null | undefined): number | null {
	if (value == null || value === "") return null;
	const n = Number(value);
	return Number.isFinite(n) ? n : null;
}

/**
 * A full {@link UpdateProfileDto} mirroring the user's current state. The update-profile
 * endpoint is a full replace, so every form that saves a subset (account, health, inline
 * name/handle) must start from this and override only its own fields — otherwise it would
 * reset everything else it omits.
 */
export function profileBasePayload(user: UserInfoDto): UpdateProfileDto {
	return {
		displayName: user.displayName ?? "",
		handle: user.handle ?? undefined,
		profileStatsPublic: user.profileStatsPublic ?? false,
		profileActivityPublic: user.profileActivityPublic ?? false,
		profileSkillPublic: user.profileSkillPublic ?? false,
		profileHistoryPublic: user.profileHistoryPublic ?? false,
		profileListsPublic: user.profileListsPublic ?? false,
		profileLikedPublic: user.profileLikedPublic ?? false,
		healthTrackingEnabled: user.healthTrackingEnabled ?? false,
		heightCm: num(user.heightCm),
		weightKg: num(user.weightKg),
		birthYear: num(user.birthYear),
		sex: user.sex ?? null,
		bodyFatPercent: num(user.bodyFatPercent),
		restingHeartRate: num(user.restingHeartRate),
	};
}

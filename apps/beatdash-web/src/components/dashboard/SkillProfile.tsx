import { useGetApiSessionsSkill } from "@/api/sessions/sessions";
import { SkillRadar } from "@/components/profile/SkillRadar";

/** The current user's skill profile, self-fetched for the dashboard. */
export function SkillProfile() {
	const query = useGetApiSessionsSkill();
	const data = query.data?.status === 200 ? query.data.data : null;
	return <SkillRadar data={data} />;
}

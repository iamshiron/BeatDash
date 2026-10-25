import { createContext, type ReactNode, useContext } from "react";
import { getGetMeQueryKey, useGetMe } from "@/api/auth/auth";
import { useGetApiHealth } from "@/api/health/health";
import type { UserInfoDto } from "@/api/model";

export { getGetMeQueryKey };

export interface AuthValue {
	user: UserInfoDto | undefined;
	isLoading: boolean;
	isAuthenticated: boolean;
	isAdmin: boolean;
	/** Single-user desktop app: the user is signed in by the app and has no account to log into or out of. */
	isDesktop: boolean;
}

const AuthContext = createContext<AuthValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
	const query = useGetMe({
		query: { retry: false, staleTime: Infinity },
	});
	const status = useGetApiHealth({ query: { staleTime: Infinity } });
	const response = query.data;
	const user = response?.status === 200 ? response.data : undefined;

	const value: AuthValue = {
		user,
		isLoading: query.isLoading || status.isLoading,
		isAuthenticated: !!user,
		isAdmin: !!user?.roles?.includes("Admin"),
		isDesktop: status.data?.data.desktop === true,
	};

	return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthValue {
	const ctx = useContext(AuthContext);
	if (!ctx) {
		throw new Error("useAuth must be used within an AuthProvider");
	}
	return ctx;
}

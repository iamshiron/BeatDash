import { createContext, useContext, type ReactNode } from "react";
import { getGetMeQueryKey, useGetMe } from "@/api/auth/auth";
import type { UserInfoDto } from "@/api/model";

export { getGetMeQueryKey };

export interface AuthValue {
	user: UserInfoDto | undefined;
	isLoading: boolean;
	isAuthenticated: boolean;
}

const AuthContext = createContext<AuthValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
	const query = useGetMe({
		query: { retry: false, staleTime: Infinity },
	});
	const response = query.data;
	const user = response?.status === 200 ? response.data : undefined;

	const value: AuthValue = {
		user,
		isLoading: query.isLoading,
		isAuthenticated: !!user,
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

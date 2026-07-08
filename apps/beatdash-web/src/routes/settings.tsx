import { Button } from "@shiron/ui/components/ui/button";
import {
	Card,
	CardContent,
	CardDescription,
	CardFooter,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import { Field, FieldGroup, FieldLabel } from "@shiron/ui/components/ui/field";
import { Input } from "@shiron/ui/components/ui/input";
import { cn } from "@shiron/ui/lib/utils";
import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, redirect } from "@tanstack/react-router";
import { useTheme } from "next-themes";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { useChangePassword, useUpdateProfile } from "@/api/auth/auth";
import type { ChangePasswordDto, UpdateProfileDto } from "@/api/model";
import { AppShell } from "@/components/layout/AppShell";
import { getGetMeQueryKey, useAuth } from "@/contexts/auth";

export const Route = createFileRoute("/settings")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	component: SettingsPage,
});

function SettingsPage() {
	return (
		<AppShell>
			<div className="mb-6">
				<h1 className="font-heading text-2xl font-bold tracking-tight">
					Settings
				</h1>
				<p className="mt-1 text-sm text-muted-foreground">
					Manage your account and preferences.
				</p>
			</div>
			<div className="flex flex-col gap-4">
				<AccountCard />
				<PasswordCard />
				<AppearanceCard />
			</div>
		</AppShell>
	);
}

function AccountCard() {
	const { user } = useAuth();
	const queryClient = useQueryClient();
	const [displayName, setDisplayName] = useState("");

	// Seed the field once the current user loads.
	useEffect(() => {
		if (user?.displayName) setDisplayName(user.displayName);
	}, [user?.displayName]);

	const updateMutation = useUpdateProfile({
		mutation: {
			onSuccess: async (response) => {
				if (response.status !== 200) {
					toast.error("Could not update your profile.");
					return;
				}
				await queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
				toast.success("Profile updated.");
			},
			onError: () => toast.error("Could not update your profile."),
		},
	});

	const trimmed = displayName.trim();
	const unchanged = trimmed === (user?.displayName ?? "");
	const invalid = trimmed.length === 0 || trimmed.length > 32;

	function handleSubmit(event: React.FormEvent) {
		event.preventDefault();
		if (invalid || unchanged) return;
		const payload: UpdateProfileDto = { displayName: trimmed };
		updateMutation.mutate({ data: payload });
	}

	return (
		<Card>
			<CardHeader>
				<CardTitle>Account</CardTitle>
				<CardDescription>
					Your display name is shown across BeatDash.
				</CardDescription>
			</CardHeader>
			<CardContent>
				<form id="account-form" onSubmit={handleSubmit}>
					<FieldGroup>
						<Field>
							<FieldLabel htmlFor="displayName">Display name</FieldLabel>
							<Input
								id="displayName"
								type="text"
								maxLength={32}
								value={displayName}
								onChange={(e) => setDisplayName(e.target.value)}
								required
							/>
						</Field>
						<Field>
							<FieldLabel htmlFor="username">Username</FieldLabel>
							<Input
								id="username"
								type="text"
								value={user?.userName ?? ""}
								disabled
								readOnly
							/>
						</Field>
						<Field>
							<FieldLabel htmlFor="email">Email</FieldLabel>
							<Input
								id="email"
								type="email"
								value={user?.email ?? ""}
								disabled
								readOnly
							/>
						</Field>
					</FieldGroup>
				</form>
			</CardContent>
			<CardFooter>
				<Button
					type="submit"
					form="account-form"
					disabled={updateMutation.isPending || invalid || unchanged}
				>
					{updateMutation.isPending ? "Saving…" : "Save changes"}
				</Button>
			</CardFooter>
		</Card>
	);
}

function PasswordCard() {
	const [currentPassword, setCurrentPassword] = useState("");
	const [newPassword, setNewPassword] = useState("");
	const [confirmPassword, setConfirmPassword] = useState("");

	const changeMutation = useChangePassword({
		mutation: {
			onSuccess: (response) => {
				if (response.status !== 200) {
					toast.error("Current password is incorrect.");
					return;
				}
				toast.success("Password changed.");
				setCurrentPassword("");
				setNewPassword("");
				setConfirmPassword("");
			},
			onError: () => toast.error("Could not change your password."),
		},
	});

	function handleSubmit(event: React.FormEvent) {
		event.preventDefault();
		if (newPassword.length < 4) {
			toast.error("New password must be at least 4 characters.");
			return;
		}
		if (newPassword !== confirmPassword) {
			toast.error("New passwords do not match.");
			return;
		}
		const payload: ChangePasswordDto = { currentPassword, newPassword };
		changeMutation.mutate({ data: payload });
	}

	return (
		<Card>
			<CardHeader>
				<CardTitle>Password</CardTitle>
				<CardDescription>
					Change the password you use to sign in.
				</CardDescription>
			</CardHeader>
			<CardContent>
				<form id="password-form" onSubmit={handleSubmit}>
					<FieldGroup>
						<Field>
							<FieldLabel htmlFor="current-password">
								Current password
							</FieldLabel>
							<Input
								id="current-password"
								type="password"
								autoComplete="current-password"
								value={currentPassword}
								onChange={(e) => setCurrentPassword(e.target.value)}
								required
							/>
						</Field>
						<Field>
							<FieldLabel htmlFor="new-password">New password</FieldLabel>
							<Input
								id="new-password"
								type="password"
								autoComplete="new-password"
								value={newPassword}
								onChange={(e) => setNewPassword(e.target.value)}
								required
							/>
						</Field>
						<Field>
							<FieldLabel htmlFor="confirm-new-password">
								Confirm new password
							</FieldLabel>
							<Input
								id="confirm-new-password"
								type="password"
								autoComplete="new-password"
								value={confirmPassword}
								onChange={(e) => setConfirmPassword(e.target.value)}
								required
							/>
						</Field>
					</FieldGroup>
				</form>
			</CardContent>
			<CardFooter>
				<Button
					type="submit"
					form="password-form"
					disabled={changeMutation.isPending}
				>
					{changeMutation.isPending ? "Changing…" : "Change password"}
				</Button>
			</CardFooter>
		</Card>
	);
}

const THEME_OPTIONS = [
	{ value: "light", label: "Light" },
	{ value: "dark", label: "Dark" },
	{ value: "system", label: "System" },
] as const;

function AppearanceCard() {
	const { theme, setTheme } = useTheme();

	return (
		<Card>
			<CardHeader>
				<CardTitle>Appearance</CardTitle>
				<CardDescription>
					Choose how BeatDash looks. Your choice is remembered on this device.
				</CardDescription>
			</CardHeader>
			<CardContent>
				<div className="flex gap-2">
					{THEME_OPTIONS.map((option) => {
						const isActive = (theme ?? "system") === option.value;
						return (
							<Button
								key={option.value}
								type="button"
								variant={isActive ? "default" : "outline"}
								size="sm"
								className={cn(!isActive && "text-muted-foreground")}
								onClick={() => setTheme(option.value)}
							>
								{option.label}
							</Button>
						);
					})}
				</div>
			</CardContent>
		</Card>
	);
}

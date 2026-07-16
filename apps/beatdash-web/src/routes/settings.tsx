import { Button } from "@shiron/ui/components/ui/button";
import {
	Card,
	CardContent,
	CardDescription,
	CardFooter,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import {
	Field,
	FieldDescription,
	FieldGroup,
	FieldLabel,
} from "@shiron/ui/components/ui/field";
import { Input } from "@shiron/ui/components/ui/input";
import { Separator } from "@shiron/ui/components/ui/separator";
import { Switch } from "@shiron/ui/components/ui/switch";
import { useTheme } from "@shiron/ui/hooks/use-theme";
import { accents, getTheme, themeForAccent } from "@shiron/ui/lib/themes";
import { cn } from "@shiron/ui/lib/utils";
import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link, redirect } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { useChangePassword, useUpdateProfile } from "@/api/auth/auth";
import type { ChangePasswordDto, UpdateProfileDto } from "@/api/model";
import { AppShell } from "@/components/layout/AppShell";
import { getGetMeQueryKey, useAuth } from "@/contexts/auth";

const HANDLE_PATTERN = /^[a-z0-9_]{3,32}$/;

/** Strips a leading "@", trims, and lowercases a handle for storage/lookup. */
function normalizeHandle(raw: string): string {
	return raw.trim().replace(/^@/, "").trim().toLowerCase();
}

/** Best-effort extraction of a server validation message from a non-200 body. */
function serverMessage(data: unknown, fallback: string): string {
	if (Array.isArray(data) && typeof data[0] === "string") return data[0];
	if (typeof data === "string" && data.length > 0) return data;
	return fallback;
}

const VISIBILITY_SECTIONS = [
	{
		key: "profileStatsPublic",
		label: "Headline stats",
		description: "Plays, accuracy, ranks and most-played maps.",
	},
	{
		key: "profileActivityPublic",
		label: "Activity",
		description: "Your play-activity heatmap.",
	},
	{
		key: "profileSkillPublic",
		label: "Skill profile",
		description: "Your skill radar across play styles.",
	},
	{
		key: "profileHistoryPublic",
		label: "Recent & best plays",
		description: "Your most recent and highest-accuracy plays.",
	},
] as const;

type VisibilityKey = (typeof VISIBILITY_SECTIONS)[number]["key"];

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
	const [handle, setHandle] = useState("");
	const [visibility, setVisibility] = useState<Record<VisibilityKey, boolean>>({
		profileStatsPublic: false,
		profileActivityPublic: false,
		profileSkillPublic: false,
		profileHistoryPublic: false,
	});

	// Seed the fields once the current user loads.
	useEffect(() => {
		if (!user) return;
		setDisplayName(user.displayName ?? "");
		setHandle(user.handle ?? "");
		setVisibility({
			profileStatsPublic: user.profileStatsPublic ?? false,
			profileActivityPublic: user.profileActivityPublic ?? false,
			profileSkillPublic: user.profileSkillPublic ?? false,
			profileHistoryPublic: user.profileHistoryPublic ?? false,
		});
	}, [user]);

	const updateMutation = useUpdateProfile({
		mutation: {
			onSuccess: async (response) => {
				if (response.status !== 200) {
					toast.error(
						serverMessage(response.data, "Could not update your profile."),
					);
					return;
				}
				await queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
				toast.success("Profile updated.");
			},
			onError: () => toast.error("Could not update your profile."),
		},
	});

	const trimmedName = displayName.trim();
	const normalizedHandle = normalizeHandle(handle);
	const nameInvalid = trimmedName.length === 0 || trimmedName.length > 32;
	const handleInvalid =
		normalizedHandle.length > 0 && !HANDLE_PATTERN.test(normalizedHandle);

	const unchanged =
		trimmedName === (user?.displayName ?? "") &&
		normalizedHandle === (user?.handle ?? "") &&
		visibility.profileStatsPublic === (user?.profileStatsPublic ?? false) &&
		visibility.profileActivityPublic ===
			(user?.profileActivityPublic ?? false) &&
		visibility.profileSkillPublic === (user?.profileSkillPublic ?? false) &&
		visibility.profileHistoryPublic === (user?.profileHistoryPublic ?? false);

	function handleSubmit(event: React.FormEvent) {
		event.preventDefault();
		if (nameInvalid || handleInvalid || unchanged) return;
		const payload: UpdateProfileDto = {
			displayName: trimmedName,
			handle: normalizedHandle || undefined,
			...visibility,
		};
		updateMutation.mutate({ data: payload });
	}

	const profilePath = user?.handle ? `/u/@${user.handle}` : null;

	function copyLink() {
		if (!profilePath) return;
		navigator.clipboard
			.writeText(`${window.location.origin}${profilePath}`)
			.then(() => toast.success("Profile link copied."))
			.catch(() => toast.error("Could not copy the link."));
	}

	return (
		<Card>
			<CardHeader>
				<CardTitle>Public profile</CardTitle>
				<CardDescription>
					Your profile lives at a shareable link. Choose what's visible — each
					section is private until you turn it on.
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
						<Field data-invalid={handleInvalid || undefined}>
							<FieldLabel htmlFor="handle">Handle</FieldLabel>
							<Input
								id="handle"
								type="text"
								maxLength={32}
								placeholder="yourhandle"
								value={handle}
								onChange={(e) => setHandle(e.target.value)}
								aria-invalid={handleInvalid || undefined}
							/>
							<FieldDescription>
								{handleInvalid
									? "3–32 characters: lowercase letters, numbers or underscores."
									: normalizedHandle
										? `Your profile: /u/@${normalizedHandle}`
										: "Pick a handle to get a shareable profile link."}
							</FieldDescription>
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

						<Separator />

						<div className="flex flex-col gap-4">
							<p className="text-sm font-medium">Visible sections</p>
							{VISIBILITY_SECTIONS.map((section) => (
								<div
									key={section.key}
									className="flex items-center justify-between gap-4"
								>
									<div className="min-w-0">
										<FieldLabel htmlFor={section.key}>
											{section.label}
										</FieldLabel>
										<p className="text-xs text-muted-foreground">
											{section.description}
										</p>
									</div>
									<Switch
										id={section.key}
										checked={visibility[section.key]}
										onCheckedChange={(checked) =>
											setVisibility((v) => ({ ...v, [section.key]: checked }))
										}
									/>
								</div>
							))}
						</div>
					</FieldGroup>
				</form>
			</CardContent>
			<CardFooter className="flex-wrap gap-2">
				<Button
					type="submit"
					form="account-form"
					disabled={
						updateMutation.isPending ||
						nameInvalid ||
						handleInvalid ||
						unchanged
					}
				>
					{updateMutation.isPending ? "Saving…" : "Save changes"}
				</Button>
				{profilePath && (
					<>
						<Button type="button" variant="outline" asChild>
							<Link to="/u/$handle" params={{ handle: `@${user?.handle}` }}>
								View profile
							</Link>
						</Button>
						<Button type="button" variant="ghost" onClick={copyLink}>
							Copy link
						</Button>
					</>
				)}
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

function AppearanceCard() {
	const { theme, setTheme, mode } = useTheme();
	const currentAccent = getTheme(theme)?.accent;

	return (
		<Card>
			<CardHeader>
				<CardTitle>Appearance</CardTitle>
				<CardDescription>
					Pick an accent colour — use the header toggle to switch between light
					and dark. Your choice is remembered on this device.
				</CardDescription>
			</CardHeader>
			<CardContent>
				<div className="flex flex-wrap gap-2">
					{accents.map((accent) => {
						// Keep the current light/dark mode when switching accent; label
						// each option with the theme name for that mode.
						const targetName = themeForAccent(accent.id, mode);
						const target = getTheme(targetName);
						const isActive = accent.id === currentAccent;
						return (
							<button
								key={accent.id}
								type="button"
								disabled={!targetName}
								onClick={() => targetName && setTheme(targetName)}
								className={cn(
									"flex items-center gap-2 rounded-lg border px-3 py-1.5 text-sm transition",
									isActive
										? "border-primary bg-primary/10 text-foreground"
										: "border-border text-muted-foreground hover:bg-muted/50",
								)}
							>
								<span
									aria-hidden
									className="size-3 rounded-full ring-1 ring-inset ring-foreground/20"
									style={{ background: target?.swatch ?? accent.swatch }}
								/>
								{target?.label ?? accent.label}
							</button>
						);
					})}
				</div>
			</CardContent>
		</Card>
	);
}

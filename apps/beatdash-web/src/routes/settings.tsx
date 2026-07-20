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
import {
	Select,
	SelectContent,
	SelectItem,
	SelectTrigger,
} from "@shiron/ui/components/ui/select";
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
import { profileBasePayload } from "@/lib/profile";

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
	{
		key: "profileListsPublic",
		label: "Playlists",
		description: "The map lists you've created.",
	},
	{
		key: "profileLikedPublic",
		label: "Liked maps",
		description: "The maps you've liked.",
	},
] as const;

type VisibilityKey = (typeof VISIBILITY_SECTIONS)[number]["key"];

import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export const Route = createFileRoute("/settings")({
	beforeLoad: ({ context }) => {
		if (!context.auth.isAuthenticated) {
			throw redirect({ to: "/auth/login", replace: true });
		}
	},
	component: SettingsPage,
});

function SettingsPage() {
	useDocumentTitle("Settings");
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
				<HealthCard />
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
		profileListsPublic: false,
		profileLikedPublic: false,
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
			profileListsPublic: user.profileListsPublic ?? false,
			profileLikedPublic: user.profileLikedPublic ?? false,
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
		visibility.profileHistoryPublic === (user?.profileHistoryPublic ?? false) &&
		visibility.profileListsPublic === (user?.profileListsPublic ?? false) &&
		visibility.profileLikedPublic === (user?.profileLikedPublic ?? false);

	function handleSubmit(event: React.FormEvent) {
		event.preventDefault();
		if (nameInvalid || handleInvalid || unchanged || !user) return;
		// Start from the full current profile so health fields aren't wiped by this
		// account-only save, then override name/handle/visibility.
		const payload: UpdateProfileDto = {
			...profileBasePayload(user),
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

const CURRENT_YEAR = new Date().getFullYear();
const KG_PER_LB = 0.45359237;
const CM_PER_IN = 2.54;

/** Parses a form string to a finite number, or null when blank/invalid. */
function numStr(value: string): number | null {
	const trimmed = value.trim();
	if (trimmed === "") return null;
	const n = Number(trimmed);
	return Number.isFinite(n) ? n : null;
}

type Unit = "metric" | "imperial";

const SEX_LABELS: Record<string, string> = {
	male: "Male",
	female: "Female",
	unspecified: "Prefer not to say",
};

function HealthCard() {
	const { user } = useAuth();
	const queryClient = useQueryClient();

	const [enabled, setEnabled] = useState(false);
	const [unit, setUnit] = useState<Unit>("metric");
	const [weight, setWeight] = useState("");
	const [heightCm, setHeightCm] = useState("");
	const [heightFt, setHeightFt] = useState("");
	const [heightIn, setHeightIn] = useState("");
	const [age, setAge] = useState("");
	const [sex, setSex] = useState(() => user?.sex || "unspecified");
	const [bodyFat, setBodyFat] = useState("");
	const [restingHr, setRestingHr] = useState("");
	const [error, setError] = useState<string | null>(null);

	// Seed from the current (metric) user data.
	useEffect(() => {
		if (!user) return;
		setEnabled(user.healthTrackingEnabled ?? false);
		const w = user.weightKg != null ? Number(user.weightKg) : null;
		const h = user.heightCm != null ? Number(user.heightCm) : null;
		const by = user.birthYear != null ? Number(user.birthYear) : null;
		setWeight(w != null ? String(Math.round(w * 10) / 10) : "");
		setHeightCm(h != null ? String(Math.round(h)) : "");
		if (h != null) {
			const totalIn = h / CM_PER_IN;
			setHeightFt(String(Math.floor(totalIn / 12)));
			setHeightIn(String(Math.round(totalIn % 12)));
		}
		setAge(by != null ? String(CURRENT_YEAR - by) : "");
		setSex(user.sex || "unspecified");
		setBodyFat(
			user.bodyFatPercent != null ? String(Number(user.bodyFatPercent)) : "",
		);
		setRestingHr(
			user.restingHeartRate != null
				? String(Number(user.restingHeartRate))
				: "",
		);
	}, [user]);

	function switchUnit(next: Unit) {
		if (next === unit) return;
		const w = numStr(weight);
		if (next === "imperial") {
			if (w != null) setWeight(String(Math.round((w / KG_PER_LB) * 10) / 10));
			const cm = numStr(heightCm);
			if (cm != null) {
				const totalIn = cm / CM_PER_IN;
				setHeightFt(String(Math.floor(totalIn / 12)));
				setHeightIn(String(Math.round(totalIn % 12)));
			}
		} else {
			if (w != null) setWeight(String(Math.round(w * KG_PER_LB * 10) / 10));
			const ft = numStr(heightFt);
			const inch = numStr(heightIn);
			if (ft != null || inch != null) {
				setHeightCm(
					String(Math.round(((ft ?? 0) * 12 + (inch ?? 0)) * CM_PER_IN)),
				);
			}
		}
		setUnit(next);
	}

	const updateMutation = useUpdateProfile({
		mutation: {
			onSuccess: async (response) => {
				if (response.status !== 200) {
					setError(
						serverMessage(response.data, "Couldn't save your health settings."),
					);
					return;
				}
				await queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
				setError(null);
				toast.success("Health settings saved.");
			},
			onError: () => setError("Couldn't save your health settings."),
		},
	});

	function handleSubmit(event: React.FormEvent) {
		event.preventDefault();
		if (!user) return;
		setError(null);

		let weightKg: number | null;
		let heightCmValue: number | null;
		const w = numStr(weight);
		if (unit === "metric") {
			weightKg = w;
			heightCmValue = numStr(heightCm);
		} else {
			weightKg = w != null ? w * KG_PER_LB : null;
			const ft = numStr(heightFt);
			const inch = numStr(heightIn);
			heightCmValue =
				ft != null || inch != null
					? ((ft ?? 0) * 12 + (inch ?? 0)) * CM_PER_IN
					: null;
		}

		const ageNum = numStr(age);
		const payload: UpdateProfileDto = {
			...profileBasePayload(user),
			healthTrackingEnabled: enabled,
			weightKg: weightKg != null ? Math.round(weightKg * 100) / 100 : null,
			heightCm: heightCmValue != null ? Math.round(heightCmValue) : null,
			birthYear: ageNum != null ? CURRENT_YEAR - Math.round(ageNum) : null,
			sex,
			bodyFatPercent: numStr(bodyFat),
			restingHeartRate: (() => {
				const r = numStr(restingHr);
				return r != null ? Math.round(r) : null;
			})(),
		};
		updateMutation.mutate({ data: payload });
	}

	return (
		<Card>
			<CardHeader>
				<CardTitle>Health &amp; fitness</CardTitle>
				<CardDescription>
					Optional. Turn this on and add your body metrics to see calories,
					active time and movement for your plays. Nothing here is shown on your
					public profile.
				</CardDescription>
			</CardHeader>
			<CardContent>
				<form id="health-form" onSubmit={handleSubmit}>
					<FieldGroup>
						<div className="flex items-center justify-between gap-4">
							<div className="min-w-0">
								<FieldLabel htmlFor="health-enabled">
									Enable health tracking
								</FieldLabel>
								<p className="text-xs text-muted-foreground">
									Shows a Health page, a dashboard card and per-play workout
									stats — only for you.
								</p>
							</div>
							<Switch
								id="health-enabled"
								checked={enabled}
								onCheckedChange={setEnabled}
							/>
						</div>

						<Separator />

						<div className="flex items-center justify-between gap-4">
							<FieldLabel>Units</FieldLabel>
							<div className="flex items-center gap-1 rounded-lg border border-border bg-muted/30 p-0.5">
								{(["metric", "imperial"] as const).map((u) => (
									<Button
										key={u}
										type="button"
										variant={unit === u ? "secondary" : "ghost"}
										size="sm"
										className="h-7 px-2.5 text-xs capitalize"
										onClick={() => switchUnit(u)}
									>
										{u}
									</Button>
								))}
							</div>
						</div>

						<div className="grid gap-4 sm:grid-cols-2">
							<Field>
								<FieldLabel htmlFor="weight">
									Weight ({unit === "metric" ? "kg" : "lb"})
								</FieldLabel>
								<Input
									id="weight"
									type="number"
									inputMode="decimal"
									step="0.1"
									value={weight}
									onChange={(e) => setWeight(e.target.value)}
								/>
							</Field>

							{unit === "metric" ? (
								<Field>
									<FieldLabel htmlFor="height-cm">Height (cm)</FieldLabel>
									<Input
										id="height-cm"
										type="number"
										inputMode="numeric"
										value={heightCm}
										onChange={(e) => setHeightCm(e.target.value)}
									/>
								</Field>
							) : (
								<Field>
									<FieldLabel htmlFor="height-ft">Height (ft / in)</FieldLabel>
									<div className="flex gap-2">
										<Input
											id="height-ft"
											type="number"
											inputMode="numeric"
											placeholder="ft"
											value={heightFt}
											onChange={(e) => setHeightFt(e.target.value)}
										/>
										<Input
											aria-label="Height inches"
											type="number"
											inputMode="numeric"
											placeholder="in"
											value={heightIn}
											onChange={(e) => setHeightIn(e.target.value)}
										/>
									</div>
								</Field>
							)}

							<Field>
								<FieldLabel htmlFor="age">Age</FieldLabel>
								<Input
									id="age"
									type="number"
									inputMode="numeric"
									value={age}
									onChange={(e) => setAge(e.target.value)}
								/>
							</Field>

							<Field>
								<FieldLabel htmlFor="sex">Sex</FieldLabel>
								<Select
									value={sex}
									// Radix can emit an empty value before the content mounts; ignore it
									// so it doesn't clobber the seeded selection.
									onValueChange={(v) => v && setSex(v)}
								>
									<SelectTrigger id="sex">
										{/* Render the label directly (not via SelectValue) so a value
										    seeded through an effect always shows — SelectValue won't
										    resolve the label until the content mounts once. */}
										<span data-slot="select-value">
											{SEX_LABELS[sex] ?? "Select…"}
										</span>
									</SelectTrigger>
									<SelectContent>
										<SelectItem value="male">Male</SelectItem>
										<SelectItem value="female">Female</SelectItem>
										<SelectItem value="unspecified">
											Prefer not to say
										</SelectItem>
									</SelectContent>
								</Select>
							</Field>
						</div>

						<Separator />

						<div>
							<p className="text-sm font-medium">Wearable data (optional)</p>
							<p className="text-xs text-muted-foreground">
								If your watch or smart scale measures these, they sharpen the
								estimates (body fat enables a more accurate BMR).
							</p>
						</div>
						<div className="grid gap-4 sm:grid-cols-2">
							<Field>
								<FieldLabel htmlFor="body-fat">Body fat (%)</FieldLabel>
								<Input
									id="body-fat"
									type="number"
									inputMode="decimal"
									step="0.1"
									value={bodyFat}
									onChange={(e) => setBodyFat(e.target.value)}
								/>
							</Field>
							<Field>
								<FieldLabel htmlFor="resting-hr">Resting HR (bpm)</FieldLabel>
								<Input
									id="resting-hr"
									type="number"
									inputMode="numeric"
									value={restingHr}
									onChange={(e) => setRestingHr(e.target.value)}
								/>
							</Field>
						</div>

						{error && <p className="text-xs text-destructive">{error}</p>}
					</FieldGroup>
				</form>

				<Separator className="my-6" />

				<div className="flex flex-col gap-1">
					<p className="text-sm font-medium">Connect a smartwatch</p>
					<p className="text-xs text-muted-foreground">
						A companion app can push live heart rate for the most accurate
						calorie estimates. Head to{" "}
						<Link to="/devices" className="text-primary hover:underline">
							Devices
						</Link>{" "}
						and choose “Link Health Proxy” to scan a pairing code.
					</p>
				</div>
			</CardContent>
			<CardFooter>
				<Button
					type="submit"
					form="health-form"
					disabled={updateMutation.isPending}
				>
					{updateMutation.isPending ? "Saving…" : "Save health settings"}
				</Button>
			</CardFooter>
		</Card>
	);
}

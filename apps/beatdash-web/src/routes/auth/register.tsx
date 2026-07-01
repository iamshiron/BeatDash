import { useState } from "react";
import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { getGetMeQueryKey, useRegister } from "@/api/auth/auth";
import type { RegisterDto } from "@/api/model";
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
import { Button } from "@shiron/ui/components/ui/button";

export const Route = createFileRoute("/auth/register")({
	component: RegisterPage,
});

function RegisterPage() {
	const [username, setUsername] = useState("");
	const [email, setEmail] = useState("");
	const [password, setPassword] = useState("");
	const [confirmPassword, setConfirmPassword] = useState("");
	const navigate = useNavigate();
	const queryClient = useQueryClient();

	const registerMutation = useRegister({
		mutation: {
			onSuccess: async (response) => {
				if (response.status !== 200) {
					toast.error(response.data?.detail ?? "Registration failed.");
					return;
				}
				await queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
				toast.success("Account created!");
				navigate({ to: "/" });
			},
		},
	});

	function handleSubmit(event: React.FormEvent) {
		event.preventDefault();
		if (password !== confirmPassword) {
			toast.error("Passwords do not match.");
			return;
		}
		const payload: RegisterDto = {
			displayName: username,
			userName: username,
			email,
			password,
		};
		registerMutation.mutate({ data: payload });
	}

	const isPending = registerMutation.isPending;

	return (
		<Card className="w-full">
			<CardHeader>
				<CardTitle>Create account</CardTitle>
				<CardDescription>
					Start tracking your Beat Saber sessions.
				</CardDescription>
			</CardHeader>
			<CardContent>
				<form id="register-form" onSubmit={handleSubmit}>
					<FieldGroup>
						<Field>
							<FieldLabel htmlFor="username">Username</FieldLabel>
							<Input
								id="username"
								type="text"
								autoComplete="username"
								value={username}
								onChange={(e) => setUsername(e.target.value)}
								required
							/>
						</Field>
						<Field>
							<FieldLabel htmlFor="email">Email</FieldLabel>
							<Input
								id="email"
								type="email"
								placeholder="you@example.com"
								autoComplete="email"
								value={email}
								onChange={(e) => setEmail(e.target.value)}
								required
							/>
						</Field>
						<Field>
							<FieldLabel htmlFor="password">Password</FieldLabel>
							<Input
								id="password"
								type="password"
								autoComplete="new-password"
								value={password}
								onChange={(e) => setPassword(e.target.value)}
								required
							/>
						</Field>
						<Field>
							<FieldLabel htmlFor="confirm-password">
								Confirm password
							</FieldLabel>
							<Input
								id="confirm-password"
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
			<CardFooter className="flex flex-col gap-3">
				<Button
					type="submit"
					form="register-form"
					size="lg"
					className="w-full"
					disabled={isPending}
				>
					{isPending ? "Creating…" : "Create account"}
				</Button>
				<p className="text-xs text-muted-foreground">
					Already have an account?{" "}
					<Link to="/auth/login" className="text-primary hover:underline">
						Sign in
					</Link>
				</p>
			</CardFooter>
		</Card>
	);
}

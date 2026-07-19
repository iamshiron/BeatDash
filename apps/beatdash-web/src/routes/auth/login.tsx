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
import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { toast } from "sonner";
import { getGetMeQueryKey, useLogin } from "@/api/auth/auth";
import type { LoginDto } from "@/api/model";

import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export const Route = createFileRoute("/auth/login")({
	component: LoginPage,
});

function LoginPage() {
	useDocumentTitle("Sign in");
	const [email, setEmail] = useState("");
	const [password, setPassword] = useState("");
	const navigate = useNavigate();
	const queryClient = useQueryClient();

	const loginMutation = useLogin({
		mutation: {
			onSuccess: async (response) => {
				if (response.status !== 200) {
					toast.error("Invalid email or password.");
					return;
				}
				await queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
				toast.success("Welcome back!");
				navigate({ to: "/" });
			},
		},
	});

	function handleSubmit(event: React.FormEvent) {
		event.preventDefault();
		const payload: LoginDto = { email, password };
		loginMutation.mutate({ data: payload });
	}

	const isPending = loginMutation.isPending;

	return (
		<Card className="w-full">
			<CardHeader>
				<CardTitle>Welcome back</CardTitle>
				<CardDescription>Sign in to your BeatDash account.</CardDescription>
			</CardHeader>
			<CardContent>
				<form id="login-form" onSubmit={handleSubmit}>
					<FieldGroup>
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
								autoComplete="current-password"
								value={password}
								onChange={(e) => setPassword(e.target.value)}
								required
							/>
						</Field>
					</FieldGroup>
				</form>
			</CardContent>
			<CardFooter className="flex flex-col gap-3">
				<Button
					type="submit"
					form="login-form"
					size="lg"
					className="w-full"
					disabled={isPending}
				>
					{isPending ? "Signing in…" : "Sign In"}
				</Button>
				<p className="text-xs text-muted-foreground">
					Don't have an account?{" "}
					<Link to="/auth/register" className="text-primary hover:underline">
						Sign up
					</Link>
				</p>
			</CardFooter>
		</Card>
	);
}

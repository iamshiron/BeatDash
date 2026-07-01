import { useState } from "react";
import { createFileRoute, Link } from "@tanstack/react-router";
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

	function handleSubmit(event: React.FormEvent) {
		event.preventDefault();
		// TODO: wire to auth API
	}

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
				<Button type="submit" form="register-form" size="lg" className="w-full">
					Create account
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

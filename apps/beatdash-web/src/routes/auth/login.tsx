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

export const Route = createFileRoute("/auth/login")({
	component: LoginPage,
});

function LoginPage() {
	const [email, setEmail] = useState("");
	const [password, setPassword] = useState("");

	function handleSubmit(event: React.FormEvent) {
		event.preventDefault();
		// TODO: wire to auth API
	}

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
				<Button type="submit" form="login-form" size="lg" className="w-full">
					Sign In
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

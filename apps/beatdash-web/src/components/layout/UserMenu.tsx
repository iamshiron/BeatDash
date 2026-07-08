import { Avatar, AvatarFallback } from "@shiron/ui/components/ui/avatar";
import { Button } from "@shiron/ui/components/ui/button";
import {
	DropdownMenu,
	DropdownMenuContent,
	DropdownMenuItem,
	DropdownMenuLabel,
	DropdownMenuSeparator,
	DropdownMenuTrigger,
} from "@shiron/ui/components/ui/dropdown-menu";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { toast } from "sonner";
import { useLogout } from "@/api/auth/auth";
import { getGetMeQueryKey, useAuth } from "@/contexts/auth";
import { getInitials } from "@/lib/user";

export function UserMenu() {
	const { user, isAdmin } = useAuth();
	const navigate = useNavigate();
	const queryClient = useQueryClient();

	const name = user?.displayName || user?.userName || "User";
	const initials = getInitials(name);

	const logoutMutation = useLogout({
		mutation: {
			onSuccess: () => {
				queryClient.setQueryData(getGetMeQueryKey(), {
					data: undefined,
					status: 401,
					headers: new Headers(),
				});
				toast.success("Signed out.");
				navigate({ to: "/auth/login" });
			},
		},
	});

	return (
		<DropdownMenu>
			<DropdownMenuTrigger asChild>
				<Button variant="ghost" className="h-8 gap-2 pl-1.5 pr-2">
					<Avatar size="sm">
						<AvatarFallback>{initials}</AvatarFallback>
					</Avatar>
					<span className="max-w-24 truncate text-xs font-medium">{name}</span>
				</Button>
			</DropdownMenuTrigger>
			<DropdownMenuContent align="end" className="min-w-52">
				<DropdownMenuLabel className="truncate">{name}</DropdownMenuLabel>
				<DropdownMenuSeparator />
				{isAdmin && (
					<>
						<DropdownMenuItem asChild>
							<Link to="/admin">Dashboard</Link>
						</DropdownMenuItem>
						<DropdownMenuSeparator />
					</>
				)}
				{user?.handle ? (
					<DropdownMenuItem asChild>
						<Link to="/u/$handle" params={{ handle: `@${user.handle}` }}>
							Profile
						</Link>
					</DropdownMenuItem>
				) : (
					<DropdownMenuItem asChild>
						<Link to="/settings">Set up profile</Link>
					</DropdownMenuItem>
				)}
				<DropdownMenuItem asChild>
					<Link to="/settings">Settings</Link>
				</DropdownMenuItem>
				<DropdownMenuSeparator />
				<DropdownMenuItem
					variant="destructive"
					onClick={() => logoutMutation.mutate()}
				>
					Sign out
				</DropdownMenuItem>
			</DropdownMenuContent>
		</DropdownMenu>
	);
}

import { Button } from "@shiron/ui/components/ui/button";
import {
	Sheet,
	SheetClose,
	SheetContent,
	SheetHeader,
	SheetTitle,
	SheetTrigger,
} from "@shiron/ui/components/ui/sheet";
import { HamburgerMenuIcon } from "@solar-icons/react/dynamic";
import { Link } from "@tanstack/react-router";
import { useState } from "react";
import { useAuth } from "@/contexts/auth";

const navItems = [
	{ to: "/", label: "Dashboard", exact: true },
	{ to: "/devices", label: "Devices", exact: false },
	{ to: "/maps", label: "Maps", exact: false },
	{ to: "/plays", label: "Plays", exact: false },
	{ to: "/lists", label: "Lists", exact: false },
	{ to: "/analysis", label: "Analysis", exact: false },
	{ to: "/live", label: "Live", exact: false },
] as const;

const linkClasses =
	"rounded-md px-3 py-2.5 text-sm font-medium text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground [&.active]:bg-accent [&.active]:text-accent-foreground";

export function MobileNav({ className }: { className?: string }) {
	const [open, setOpen] = useState(false);
	const { isAdmin } = useAuth();

	return (
		<Sheet open={open} onOpenChange={setOpen}>
			<SheetTrigger asChild>
				<Button
					variant="ghost"
					size="icon"
					className={className}
					aria-label="Open navigation menu"
				>
					<HamburgerMenuIcon size={20} />
				</Button>
			</SheetTrigger>
			<SheetContent side="right" className="w-72 max-w-[85vw]">
				<SheetHeader>
					<SheetTitle className="font-heading">BeatDash</SheetTitle>
				</SheetHeader>
				<nav className="flex flex-col gap-1 px-2">
					{navItems.map((item) => (
						<SheetClose asChild key={item.to}>
							<Link
								to={item.to}
								activeOptions={{ exact: item.exact }}
								className={linkClasses}
							>
								{item.label}
							</Link>
						</SheetClose>
					))}
					{isAdmin && (
						<SheetClose asChild>
							<Link to="/admin" className={linkClasses}>
								Admin
							</Link>
						</SheetClose>
					)}
				</nav>
			</SheetContent>
		</Sheet>
	);
}

import { ThemeToggle } from "@shiron/ui/components/ui/theme-toggle";

/**
 * Header light/dark switch. Cycles between the default themes — Amethyst
 * (dark) and Jasper (light) — persisting the choice via the library's
 * theme controller (localStorage).
 */
export function ModeToggle() {
	return <ThemeToggle accent="purple" variant="ghost" />;
}

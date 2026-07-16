import { ThemeToggle } from "@shiron/ui/components/ui/theme-toggle";
import { useTheme } from "@shiron/ui/hooks/use-theme";
import { getTheme } from "@shiron/ui/lib/themes";

/**
 * Header light/dark switch. Flips between the light and dark themes of the
 * user's currently-selected accent (default purple, i.e. Amethyst ⇄ Jasper),
 * persisting the choice via the library's theme controller (localStorage).
 * The accent itself is chosen in Settings.
 */
export function ModeToggle() {
	const { theme } = useTheme();
	const accent = getTheme(theme)?.accent ?? "purple";

	return <ThemeToggle accent={accent} />;
}

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

	// Solar icons size via the --solar-size custom property (inline style beats
	// the button's size-* class), so constrain them here.
	return (
		<ThemeToggle
			accent={accent}
			variant="ghost"
			className="[--solar-size:1.125rem]"
		/>
	);
}

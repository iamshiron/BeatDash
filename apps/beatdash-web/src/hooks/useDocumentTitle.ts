import { useEffect } from "react";

const APP_NAME = "BeatDash";

/**
 * Sets `document.title` to "<title> - BeatDash" while mounted, falling back to
 * just "BeatDash" when the title is empty (e.g. still loading). Restores the
 * bare app name on unmount.
 */
export function useDocumentTitle(title?: string | null): void {
	useEffect(() => {
		document.title = title ? `${title} - ${APP_NAME}` : APP_NAME;
		return () => {
			document.title = APP_NAME;
		};
	}, [title]);
}

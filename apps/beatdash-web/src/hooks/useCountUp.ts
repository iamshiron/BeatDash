import { useEffect, useRef, useState } from "react";

/** Whether the user has asked the OS to minimise non-essential motion. */
function prefersReducedMotion(): boolean {
	return (
		typeof window !== "undefined" &&
		window.matchMedia("(prefers-reduced-motion: reduce)").matches
	);
}

/** Decelerating ease so the value rushes in and settles gently. */
function easeOutCubic(t: number): number {
	return 1 - (1 - t) ** 3;
}

/**
 * Animates a number from its previously displayed value towards `target` over
 * `durationMs`. Honours `prefers-reduced-motion` by snapping straight to the
 * target, and re-animates smoothly from the current position whenever the
 * target changes (e.g. after a refetch).
 */
export function useCountUp(target: number, durationMs = 800): number {
	const reduce = prefersReducedMotion();
	const [display, setDisplay] = useState(reduce ? target : 0);
	// Keep the latest rendered value available to the animation without
	// re-running the effect on every frame.
	const displayRef = useRef(display);
	displayRef.current = display;

	useEffect(() => {
		if (reduce || !Number.isFinite(target)) {
			setDisplay(target);
			return;
		}
		const from = displayRef.current;
		const start = performance.now();
		let frame = 0;
		const tick = (now: number) => {
			const t = Math.min(1, (now - start) / durationMs);
			setDisplay(from + (target - from) * easeOutCubic(t));
			if (t < 1) frame = requestAnimationFrame(tick);
		};
		frame = requestAnimationFrame(tick);
		return () => cancelAnimationFrame(frame);
	}, [target, durationMs, reduce]);

	return display;
}

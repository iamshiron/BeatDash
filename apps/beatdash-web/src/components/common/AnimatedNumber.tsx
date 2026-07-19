import { useCountUp } from "@/hooks/useCountUp";

interface AnimatedNumberProps {
	/** The target value to count towards. */
	value: number;
	/**
	 * Formats the (fractional, mid-animation) value for display. Round inside the
	 * formatter for integer counts, e.g. `(n) => formatScore(Math.round(n))`.
	 * Defaults to a rounded, thousands-separated integer.
	 */
	format?: (value: number) => string;
	/** Animation length in milliseconds. */
	duration?: number;
	className?: string;
}

/** A number that counts up to its value on mount and whenever it changes. */
export function AnimatedNumber({
	value,
	format,
	duration,
	className,
}: AnimatedNumberProps) {
	const animated = useCountUp(value, duration);
	return (
		<span className={className}>
			{format ? format(animated) : Math.round(animated).toLocaleString("en-US")}
		</span>
	);
}

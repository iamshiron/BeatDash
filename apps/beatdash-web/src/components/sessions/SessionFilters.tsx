import { Calendar as CalendarIcon, Filter } from "@solar-icons/react";
import { Badge } from "@shiron/ui/components/ui/badge";
import { Button } from "@shiron/ui/components/ui/button";
import { Calendar } from "@shiron/ui/components/ui/calendar";
import { Checkbox } from "@shiron/ui/components/ui/checkbox";
import { Input } from "@shiron/ui/components/ui/input";
import { Label } from "@shiron/ui/components/ui/label";
import {
	Popover,
	PopoverContent,
	PopoverTrigger,
} from "@shiron/ui/components/ui/popover";
import {
	Select,
	SelectContent,
	SelectItem,
	SelectTrigger,
	SelectValue,
} from "@shiron/ui/components/ui/select";
import { Separator } from "@shiron/ui/components/ui/separator";
import { Slider } from "@shiron/ui/components/ui/slider";
import { cn } from "@shiron/ui/lib/utils";
import { useDebouncedCallback } from "@tanstack/react-pacer";
import { format } from "date-fns";
import { useEffect, useState } from "react";
import {
	DIFFICULTY_OPTIONS,
	getActiveFilters,
	OUTCOME_OPTIONS,
	RANK_OPTIONS,
	type SessionSearchParams,
} from "@/lib/sessions";

const NUMERIC_DEBOUNCE_MS = 350;

interface SessionFiltersProps {
	search: SessionSearchParams;
	/** Applies a partial filter update (the caller resets the page). */
	onChange: (updates: Partial<SessionSearchParams>) => void;
	/** Clears every filter at once. */
	onReset: () => void;
}

/**
 * The Sessions filter panel: a "Filters" trigger with an active-count badge that
 * opens a popover of every map/session filter. All state is owned by the URL via
 * {@link SessionFiltersProps.onChange}; local state exists only to keep text/slider
 * inputs responsive before their debounced commit.
 */
export function SessionFilters({
	search,
	onChange,
	onReset,
}: SessionFiltersProps) {
	const activeCount = getActiveFilters(search).length;

	return (
		<Popover>
			<PopoverTrigger asChild>
				<Button type="button" variant="outline" className="h-9 gap-1.5">
					<Filter className="size-4" />
					Filters
					{activeCount > 0 && (
						<Badge
							variant="secondary"
							className="ml-0.5 h-5 min-w-5 justify-center rounded-full px-1 font-mono text-[0.65rem] tabular-nums"
						>
							{activeCount}
						</Badge>
					)}
				</Button>
			</PopoverTrigger>
			<PopoverContent
				align="end"
				className="max-h-[min(32rem,70vh)] w-80 overflow-y-auto"
			>
				<div className="flex items-center justify-between">
					<h3 className="font-heading text-sm font-semibold">Filters</h3>
					<Button
						type="button"
						variant="ghost"
						size="sm"
						className="h-7 px-2 text-xs text-muted-foreground"
						disabled={activeCount === 0}
						onClick={onReset}
					>
						Reset
					</Button>
				</div>

				<Separator className="my-3" />

				<div className="flex flex-col gap-4">
					<Field label="Difficulty">
						<Select
							value={search.diff ?? "all"}
							onValueChange={(v) =>
								onChange({ diff: v === "all" ? undefined : v })
							}
						>
							<SelectTrigger className="h-8 w-full text-xs">
								<SelectValue placeholder="All difficulties" />
							</SelectTrigger>
							<SelectContent>
								<SelectItem value="all">All difficulties</SelectItem>
								{DIFFICULTY_OPTIONS.map((opt) => (
									<SelectItem key={opt.value} value={opt.value}>
										{opt.label}
									</SelectItem>
								))}
							</SelectContent>
						</Select>
					</Field>

					<Field label="Rank">
						<Select
							value={search.rank ?? "all"}
							onValueChange={(v) =>
								onChange({ rank: v === "all" ? undefined : v })
							}
						>
							<SelectTrigger className="h-8 w-full text-xs">
								<SelectValue placeholder="Any rank" />
							</SelectTrigger>
							<SelectContent>
								<SelectItem value="all">Any rank</SelectItem>
								{RANK_OPTIONS.map((opt) => (
									<SelectItem key={opt.value} value={opt.value}>
										{opt.label}
									</SelectItem>
								))}
							</SelectContent>
						</Select>
					</Field>

					<DateRangeField search={search} onChange={onChange} />

					<Separator />

					<AccuracyField search={search} onChange={onChange} />

					<RangeField
						label="Score"
						minValue={search.smin}
						maxValue={search.smax}
						onChange={(min, max) => onChange({ smin: min, smax: max })}
					/>

					<RangeField
						label="BPM"
						minValue={search.bmin}
						maxValue={search.bmax}
						onChange={(min, max) => onChange({ bmin: min, bmax: max })}
					/>

					<Separator />

					<Field label="Outcomes">
						<div className="grid grid-cols-2 gap-2">
							{OUTCOME_OPTIONS.map(({ key, label }) => (
								<div key={key} className="flex items-center gap-2">
									<Checkbox
										id={`outcome-${key}`}
										checked={Boolean(search[key])}
										onCheckedChange={(checked) =>
											onChange({ [key]: checked === true })
										}
									/>
									<Label
										htmlFor={`outcome-${key}`}
										className="cursor-pointer font-normal"
									>
										{label}
									</Label>
								</div>
							))}
						</div>
					</Field>

					<div className="flex items-center gap-2">
						<Checkbox
							id="filter-fc"
							checked={Boolean(search.fc)}
							onCheckedChange={(checked) => onChange({ fc: checked === true })}
						/>
						<Label htmlFor="filter-fc" className="cursor-pointer font-normal">
							Full combo only
						</Label>
					</div>
				</div>
			</PopoverContent>
		</Popover>
	);
}

/** A labelled filter row. */
function Field({
	label,
	children,
}: {
	label: string;
	children: React.ReactNode;
}) {
	return (
		<div className="flex flex-col gap-1.5">
			<Label className="text-muted-foreground">{label}</Label>
			{children}
		</div>
	);
}

/** Played-date range, picked from a calendar and stored as `YYYY-MM-DD` strings. */
function DateRangeField({
	search,
	onChange,
}: {
	search: SessionSearchParams;
	onChange: (updates: Partial<SessionSearchParams>) => void;
}) {
	const from = parseDay(search.from);
	const to = parseDay(search.to);
	const hasRange = Boolean(search.from || search.to);
	const label = search.from
		? search.to
			? `${search.from} – ${search.to}`
			: `From ${search.from}`
		: search.to
			? `Until ${search.to}`
			: "Any date";

	return (
		<Field label="Played between">
			<Popover>
				<PopoverTrigger asChild>
					<Button
						type="button"
						variant="outline"
						className="h-8 w-full justify-start gap-2 px-2.5 text-xs font-normal"
					>
						<CalendarIcon className="size-4 text-muted-foreground" />
						<span className={cn(!hasRange && "text-muted-foreground")}>
							{label}
						</span>
					</Button>
				</PopoverTrigger>
				<PopoverContent align="start" className="w-auto p-0">
					<Calendar
						mode="range"
						numberOfMonths={1}
						autoFocus
						selected={{ from, to }}
						onSelect={(range) =>
							onChange({
								from: formatDay(range?.from),
								to: formatDay(range?.to),
							})
						}
					/>
					{hasRange && (
						<div className="border-t border-border p-2">
							<Button
								type="button"
								variant="ghost"
								size="sm"
								className="w-full text-xs text-muted-foreground"
								onClick={() => onChange({ from: undefined, to: undefined })}
							>
								Clear dates
							</Button>
						</div>
					)}
				</PopoverContent>
			</Popover>
		</Field>
	);
}

/** Accuracy range as a dual-thumb slider over 0–100%, committed on release. */
function AccuracyField({
	search,
	onChange,
}: {
	search: SessionSearchParams;
	onChange: (updates: Partial<SessionSearchParams>) => void;
}) {
	const lo = search.amin ?? 0;
	const hi = search.amax ?? 100;
	const [range, setRange] = useState<[number, number]>([lo, hi]);

	// Keep the slider in sync when the URL changes externally (chip removal, back button).
	useEffect(() => setRange([lo, hi]), [lo, hi]);

	return (
		<div className="flex flex-col gap-2">
			<div className="flex items-center justify-between">
				<Label className="text-muted-foreground">Accuracy</Label>
				<span className="font-mono text-xs tabular-nums text-muted-foreground">
					{range[0]}% – {range[1]}%
				</span>
			</div>
			<Slider
				min={0}
				max={100}
				step={1}
				value={range}
				onValueChange={(v) => setRange([v[0] ?? 0, v[1] ?? 100])}
				onValueCommit={(v) => {
					const min = v[0] ?? 0;
					const max = v[1] ?? 100;
					onChange({
						amin: min <= 0 ? undefined : min,
						amax: max >= 100 ? undefined : max,
					});
				}}
			/>
		</div>
	);
}

/** A min/max pair of numeric inputs that commits parsed values after a debounce. */
function RangeField({
	label,
	minValue,
	maxValue,
	onChange,
}: {
	label: string;
	minValue?: number;
	maxValue?: number;
	onChange: (min: number | undefined, max: number | undefined) => void;
}) {
	const [minRaw, setMinRaw] = useState(minValue?.toString() ?? "");
	const [maxRaw, setMaxRaw] = useState(maxValue?.toString() ?? "");

	// Reflect external URL changes back into the inputs.
	useEffect(() => setMinRaw(minValue?.toString() ?? ""), [minValue]);
	useEffect(() => setMaxRaw(maxValue?.toString() ?? ""), [maxValue]);

	const commit = useDebouncedCallback(
		(minStr: string, maxStr: string) =>
			onChange(parseNumber(minStr), parseNumber(maxStr)),
		{ wait: NUMERIC_DEBOUNCE_MS },
	);

	return (
		<Field label={label}>
			<div className="flex items-center gap-2">
				<Input
					type="number"
					inputMode="numeric"
					placeholder="Min"
					aria-label={`Minimum ${label}`}
					value={minRaw}
					onChange={(e) => {
						setMinRaw(e.target.value);
						commit(e.target.value, maxRaw);
					}}
					className="h-8 text-xs"
				/>
				<span className="text-xs text-muted-foreground">–</span>
				<Input
					type="number"
					inputMode="numeric"
					placeholder="Max"
					aria-label={`Maximum ${label}`}
					value={maxRaw}
					onChange={(e) => {
						setMaxRaw(e.target.value);
						commit(minRaw, e.target.value);
					}}
					className="h-8 text-xs"
				/>
			</div>
		</Field>
	);
}

/** Parses an input string to a finite number, or `undefined` when empty/invalid. */
function parseNumber(value: string): number | undefined {
	if (value.trim() === "") return undefined;
	const n = Number(value);
	return Number.isFinite(n) ? n : undefined;
}

/** Parses a `YYYY-MM-DD` string into a local `Date`, or `undefined`. */
function parseDay(value: string | undefined): Date | undefined {
	if (!value) return undefined;
	const [y, m, d] = value.split("-").map(Number);
	if (!y || !m || !d) return undefined;
	return new Date(y, m - 1, d);
}

/** Formats a `Date` as a `YYYY-MM-DD` string, or `undefined`. */
function formatDay(date: Date | undefined): string | undefined {
	return date ? format(date, "yyyy-MM-dd") : undefined;
}

import { Badge } from "@shiron/ui/components/ui/badge";
import { Button } from "@shiron/ui/components/ui/button";
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
import { FilterIcon } from "@solar-icons/react/dynamic";
import { useRouter } from "@tanstack/react-router";
import { useCallback, useEffect, useRef, useState } from "react";
import {
	type BooleanFilter,
	DIFFICULTY_OPTIONS,
	FETCH_STATUS_OPTIONS,
	getActiveMapFilters,
	type MapRangeKind,
	type MapsSearch,
	parseMapRangeInput,
} from "@/lib/maps";

const INPUT_DEBOUNCE_MS = 350;

interface MapFiltersProps {
	search: MapsSearch;
	onChange: (updates: Partial<MapsSearch>) => void;
	onReset: () => void;
}

/** Advanced catalog filters backed by the validated maps URL state. */
export function MapFilters({ search, onChange, onReset }: MapFiltersProps) {
	const activeCount = getActiveMapFilters(search).length;

	return (
		<Popover>
			<PopoverTrigger asChild>
				<Button type="button" variant="outline" className="h-9 gap-1.5">
					<FilterIcon className="size-4" />
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
				className="max-h-[min(36rem,70vh)] w-80 overflow-y-auto"
			>
				<div className="flex items-center justify-between">
					<h2 className="font-heading text-sm font-semibold">Map filters</h2>
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
					<TextField
						id="map-characteristic"
						label="Characteristic"
						value={search.characteristic}
						placeholder="For example, Standard"
						onChange={(characteristic) => onChange({ characteristic })}
					/>
					<Field label="Difficulty" htmlFor="map-difficulty">
						<Select
							value={search.difficulty ?? "all"}
							onValueChange={(value) =>
								onChange({
									difficulty:
										value === "all"
											? undefined
											: DIFFICULTY_OPTIONS.find(
													(option) => option.value === value,
												)?.value,
								})
							}
						>
							<SelectTrigger className="h-8 w-full text-xs" id="map-difficulty">
								<SelectValue placeholder="All difficulties" />
							</SelectTrigger>
							<SelectContent>
								<SelectItem value="all">All difficulties</SelectItem>
								{DIFFICULTY_OPTIONS.map((option) => (
									<SelectItem key={option.value} value={option.value}>
										{option.label}
									</SelectItem>
								))}
							</SelectContent>
						</Select>
					</Field>

					<RangeField
						label="BPM"
						minValue={search.bpmMin}
						maxValue={search.bpmMax}
						onChange={(bpmMin, bpmMax) => onChange({ bpmMin, bpmMax })}
					/>
					<RangeField
						label="Duration (seconds)"
						kind="duration"
						minValue={search.durationMin}
						maxValue={search.durationMax}
						onChange={(durationMin, durationMax) =>
							onChange({ durationMin, durationMax })
						}
					/>
					<RangeField
						label="Notes per second"
						minValue={search.npsMin}
						maxValue={search.npsMax}
						step="0.1"
						onChange={(npsMin, npsMax) => onChange({ npsMin, npsMax })}
					/>
					<RangeField
						label="Difficulty rating"
						kind="rating"
						minValue={search.ratingMin}
						maxValue={search.ratingMax}
						step="0.01"
						onChange={(ratingMin, ratingMax) =>
							onChange({ ratingMin, ratingMax })
						}
					/>
					<RangeField
						label="Performance points"
						kind="number"
						minValue={search.ppMin}
						maxValue={search.ppMax}
						step="0.1"
						onChange={(ppMin, ppMax) => onChange({ ppMin, ppMax })}
					/>

					<Separator />

					<BooleanSelect
						id="map-ranked"
						label="Ranked"
						value={search.ranked}
						onChange={(ranked) => onChange({ ranked })}
					/>
					<BooleanSelect
						id="map-automapper"
						label="Automapper"
						value={search.automapper}
						onChange={(automapper) => onChange({ automapper })}
					/>
					<TextField
						id="map-tag"
						label="BeatSaver tag"
						value={search.tag}
						placeholder="For example, nightcore"
						onChange={(tag) => onChange({ tag })}
					/>
					<Field label="Metadata status" htmlFor="map-fetch-status">
						<Select
							value={search.fetchStatus ?? "all"}
							onValueChange={(value) =>
								onChange({
									fetchStatus:
										value === "all"
											? undefined
											: FETCH_STATUS_OPTIONS.find(
													(option) => option.value === value,
												)?.value,
								})
							}
						>
							<SelectTrigger
								className="h-8 w-full text-xs"
								id="map-fetch-status"
							>
								<SelectValue placeholder="Any status" />
							</SelectTrigger>
							<SelectContent>
								<SelectItem value="all">Any status</SelectItem>
								{FETCH_STATUS_OPTIONS.map((option) => (
									<SelectItem key={option.value} value={option.value}>
										{option.label}
									</SelectItem>
								))}
							</SelectContent>
						</Select>
					</Field>
				</div>
			</PopoverContent>
		</Popover>
	);
}

function Field({
	label,
	htmlFor,
	children,
}: {
	label: string;
	htmlFor?: string;
	children: React.ReactNode;
}) {
	return (
		<div className="flex flex-col gap-1.5">
			<Label htmlFor={htmlFor} className="text-muted-foreground">
				{label}
			</Label>
			{children}
		</div>
	);
}

function TextField({
	id,
	label,
	value,
	placeholder,
	onChange,
}: {
	id: string;
	label: string;
	value: string | undefined;
	placeholder: string;
	onChange: (value: string | undefined) => void;
}) {
	const [input, setInput] = useState(value ?? "");
	const { cancel, schedule } = useDebouncedChange((next: string) =>
		onChange(next.trim() || undefined),
	);
	const discard = useCallback(() => {
		setInput(value ?? "");
		cancel();
	}, [value, cancel]);
	useEffect(discard, [discard]);
	useDiscardOnNavigation(discard);
	return (
		<Field label={label} htmlFor={id}>
			<Input
				id={id}
				value={input}
				placeholder={placeholder}
				className="h-8 text-xs"
				onChange={(event) => {
					setInput(event.target.value);
					schedule(event.target.value);
				}}
			/>
		</Field>
	);
}

function RangeField({
	label,
	minValue,
	maxValue,
	step = "1",
	kind = "float",
	onChange,
}: {
	label: string;
	minValue: number | undefined;
	maxValue: number | undefined;
	step?: string;
	kind?: MapRangeKind;
	onChange: (min: number | undefined, max: number | undefined) => void;
}) {
	const [min, setMin] = useState(minValue?.toString() ?? "");
	const [max, setMax] = useState(maxValue?.toString() ?? "");
	const minInput = parseMapRangeInput(min, kind);
	const maxInput = parseMapRangeInput(max, kind);
	const isInverted =
		minInput.valid &&
		maxInput.valid &&
		minInput.value !== undefined &&
		maxInput.value !== undefined &&
		minInput.value > maxInput.value;
	const isInvalid = !minInput.valid || !maxInput.valid || isInverted;
	const { cancel, schedule } = useDebouncedChange(
		(nextMin: string, nextMax: string) => {
			const parsedMin = parseMapRangeInput(nextMin, kind);
			const parsedMax = parseMapRangeInput(nextMax, kind);
			if (
				!parsedMin.valid ||
				!parsedMax.valid ||
				(parsedMin.value !== undefined &&
					parsedMax.value !== undefined &&
					parsedMin.value > parsedMax.value)
			) {
				return;
			}
			onChange(parsedMin.value, parsedMax.value);
		},
	);
	const discard = useCallback(() => {
		setMin(minValue?.toString() ?? "");
		setMax(maxValue?.toString() ?? "");
		cancel();
	}, [minValue, maxValue, cancel]);
	useEffect(discard, [discard]);
	useDiscardOnNavigation(discard);
	return (
		<Field label={label}>
			<div className="grid grid-cols-2 gap-2">
				<Input
					aria-label={`Minimum ${label}`}
					type="number"
					min="0"
					max={
						kind === "rating"
							? 1
							: kind === "duration"
								? 2_147_483_647
								: undefined
					}
					step={step}
					value={min}
					placeholder="Min"
					aria-invalid={isInvalid}
					className="h-8 text-xs"
					onChange={(event) => {
						const next = event.target.value;
						setMin(next);
						schedule(next, max);
					}}
				/>
				<Input
					aria-label={`Maximum ${label}`}
					type="number"
					min="0"
					max={
						kind === "rating"
							? 1
							: kind === "duration"
								? 2_147_483_647
								: undefined
					}
					step={step}
					value={max}
					placeholder="Max"
					aria-invalid={isInvalid}
					className="h-8 text-xs"
					onChange={(event) => {
						const next = event.target.value;
						setMax(next);
						schedule(min, next);
					}}
				/>
			</div>
			{isInverted && (
				<p className="text-xs text-destructive" role="alert">
					Minimum cannot exceed maximum.
				</p>
			)}
			{(!minInput.valid || !maxInput.valid) && (
				<p className="text-xs text-destructive" role="alert">
					{kind === "rating"
						? "Enter a rating between 0 and 1."
						: kind === "duration"
							? "Enter whole seconds between 0 and 2147483647."
							: "Enter a non-negative number within the supported range."}
				</p>
			)}
		</Field>
	);
}

function BooleanSelect({
	label,
	id,
	value,
	onChange,
}: {
	label: string;
	id: string;
	value: BooleanFilter | undefined;
	onChange: (value: BooleanFilter | undefined) => void;
}) {
	return (
		<Field label={label} htmlFor={id}>
			<Select
				value={value ?? "all"}
				onValueChange={(next) =>
					onChange(
						next === "all"
							? undefined
							: next === "yes" || next === "no"
								? next
								: undefined,
					)
				}
			>
				<SelectTrigger className="h-8 w-full text-xs" id={id}>
					<SelectValue placeholder={`Any ${label.toLowerCase()} maps`} />
				</SelectTrigger>
				<SelectContent>
					<SelectItem value="all">Any</SelectItem>
					<SelectItem value="yes">Yes</SelectItem>
					<SelectItem value="no">No</SelectItem>
				</SelectContent>
			</Select>
		</Field>
	);
}

function useDiscardOnNavigation(discard: () => void) {
	const router = useRouter();
	const callback = useRef(discard);
	callback.current = discard;
	useEffect(
		() => router.subscribe("onBeforeNavigate", () => callback.current()),
		[router],
	);
}

function useDebouncedChange<TArgs extends unknown[]>(
	onChange: (...args: TArgs) => void,
) {
	const timeout = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
	const callback = useRef(onChange);
	callback.current = onChange;

	useEffect(
		() => () => {
			if (timeout.current !== undefined) clearTimeout(timeout.current);
		},
		[],
	);

	const cancel = useCallback(() => {
		if (timeout.current !== undefined) clearTimeout(timeout.current);
		timeout.current = undefined;
	}, []);
	const schedule = useCallback((...args: TArgs) => {
		if (timeout.current !== undefined) clearTimeout(timeout.current);
		timeout.current = setTimeout(
			() => callback.current(...args),
			INPUT_DEBOUNCE_MS,
		);
	}, []);
	return { cancel, schedule };
}

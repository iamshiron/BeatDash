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
import { useDebouncedCallback } from "@tanstack/react-pacer";
import { useEffect, useState } from "react";
import {
	DIFFICULTY_OPTIONS,
	FETCH_STATUS_OPTIONS,
	getActiveMapFilters,
	type BooleanFilter,
	type MapsSearch,
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
					<Field label="Difficulty">
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
						minValue={search.ratingMin}
						maxValue={search.ratingMax}
						step="0.01"
						onChange={(ratingMin, ratingMax) =>
							onChange({ ratingMin, ratingMax })
						}
					/>
					<RangeField
						label="Performance points"
						minValue={search.ppMin}
						maxValue={search.ppMax}
						step="0.1"
						onChange={(ppMin, ppMax) => onChange({ ppMin, ppMax })}
					/>

					<Separator />

					<BooleanSelect
						label="Ranked"
						value={search.ranked}
						onChange={(ranked) => onChange({ ranked })}
					/>
					<BooleanSelect
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
					<Field label="Metadata status">
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
	useEffect(() => setInput(value ?? ""), [value]);
	const commit = useDebouncedCallback(
		(next: string) => onChange(next.trim() || undefined),
		{ wait: INPUT_DEBOUNCE_MS },
	);
	return (
		<Field label={label}>
			<Input
				id={id}
				value={input}
				placeholder={placeholder}
				className="h-8 text-xs"
				onChange={(event) => {
					setInput(event.target.value);
					commit(event.target.value);
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
	onChange,
}: {
	label: string;
	minValue: number | undefined;
	maxValue: number | undefined;
	step?: string;
	onChange: (min: number | undefined, max: number | undefined) => void;
}) {
	const [min, setMin] = useState(minValue?.toString() ?? "");
	const [max, setMax] = useState(maxValue?.toString() ?? "");
	useEffect(() => {
		setMin(minValue?.toString() ?? "");
		setMax(maxValue?.toString() ?? "");
	}, [minValue, maxValue]);
	const commit = useDebouncedCallback(
		(nextMin: string, nextMax: string) =>
			onChange(toNumber(nextMin), toNumber(nextMax)),
		{ wait: INPUT_DEBOUNCE_MS },
	);
	return (
		<Field label={label}>
			<div className="grid grid-cols-2 gap-2">
				<Input
					aria-label={`Minimum ${label}`}
					type="number"
					min="0"
					step={step}
					value={min}
					placeholder="Min"
					className="h-8 text-xs"
					onChange={(event) => {
						const next = event.target.value;
						setMin(next);
						commit(next, max);
					}}
				/>
				<Input
					aria-label={`Maximum ${label}`}
					type="number"
					min="0"
					step={step}
					value={max}
					placeholder="Max"
					className="h-8 text-xs"
					onChange={(event) => {
						const next = event.target.value;
						setMax(next);
						commit(min, next);
					}}
				/>
			</div>
		</Field>
	);
}

function BooleanSelect({
	label,
	value,
	onChange,
}: {
	label: string;
	value: BooleanFilter | undefined;
	onChange: (value: BooleanFilter | undefined) => void;
}) {
	return (
		<Field label={label}>
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
				<SelectTrigger className="h-8 w-full text-xs">
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

function toNumber(value: string): number | undefined {
	if (value.trim() === "") return undefined;
	const number = Number(value);
	return Number.isFinite(number) && number >= 0 ? number : undefined;
}

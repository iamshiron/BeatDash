import {
	AddCircleIcon,
	CheckReadIcon,
	CloseCircleIcon,
	CopyIcon,
	MagnifierIcon,
	RestartIcon,
} from "@solar-icons/react/dynamic";
import { Badge } from "@shiron/ui/components/ui/badge";
import { Button } from "@shiron/ui/components/ui/button";
import {
	Card,
	CardContent,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import {
	Dialog,
	DialogContent,
	DialogDescription,
	DialogHeader,
	DialogTitle,
	DialogTrigger,
} from "@shiron/ui/components/ui/dialog";
import { Input } from "@shiron/ui/components/ui/input";
import {
	NativeSelect,
	NativeSelectOption,
} from "@shiron/ui/components/ui/native-select";
import {
	Tabs,
	TabsContent,
	TabsList,
	TabsTrigger,
} from "@shiron/ui/components/ui/tabs";
import { cn } from "@shiron/ui/lib/utils";
import { useDebouncedCallback } from "@tanstack/react-pacer";
import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useId, useMemo, useState } from "react";
import {
	useGetApiAdminMetricsConfig,
	usePostApiAdminMetricsScore,
} from "@/api/admin/admin";
import { useGetApiMaps } from "@/api/maps/maps";
import type {
	FeatureCatalogItemDto,
	MetricConfig,
	ScoreDifficultyDto,
	ScoreMapDto,
	WeightedModel,
} from "@/api/model";

export const Route = createFileRoute("/admin/scoring")({
	component: ScoringLabPage,
});

const SEARCH_DEBOUNCE_MS = 250;
const SCORE_DEBOUNCE_MS = 200;

const RANK_STYLES: Record<string, string> = {
	Easy: "border-emerald-500/25 bg-emerald-500/15 text-emerald-400",
	Normal: "border-sky-500/25 bg-sky-500/15 text-sky-400",
	Hard: "border-amber-500/25 bg-amber-500/15 text-amber-400",
	Expert: "border-rose-500/25 bg-rose-500/15 text-rose-400",
	ExpertPlus: "border-violet-500/25 bg-violet-500/15 text-violet-400",
};
const RANK_LABELS: Record<string, string> = { ExpertPlus: "Expert+" };

// ---------------------------------------------------------------------------
// Draft model — values are kept as strings so intermediate edits ("-", "0.",
// "1e-3") stay editable; they're coerced to numbers only when a request is built.
// ---------------------------------------------------------------------------

type DraftWeight = { key: string; value: string };
type DraftModel = { weights: DraftWeight[]; bias: string; scale: string };
type Draft = {
	difficulty: DraftModel;
	pp: { multiplier: string; exponent: string };
	characteristics: { name: string; model: DraftModel }[];
};

function num(s: string): number {
	const v = Number(s);
	return Number.isFinite(v) ? v : 0;
}

function toDraftModel(m: WeightedModel | undefined): DraftModel {
	return {
		weights: Object.entries(m?.weights ?? {}).map(([key, value]) => ({
			key,
			value: String(value),
		})),
		bias: String(m?.bias ?? 0),
		scale: String(m?.scale ?? 1),
	};
}

function toDraft(c: MetricConfig): Draft {
	return {
		difficulty: toDraftModel(c.difficulty),
		pp: {
			multiplier: String(c.pp?.multiplier ?? 500),
			exponent: String(c.pp?.exponent ?? 2.5),
		},
		characteristics: Object.entries(c.characteristics ?? {}).map(
			([name, model]) => ({ name, model: toDraftModel(model) }),
		),
	};
}

function modelToConfig(m: DraftModel): WeightedModel {
	const weights: Record<string, number> = {};
	for (const w of m.weights) if (w.key) weights[w.key] = num(w.value);
	return { weights, bias: num(m.bias), scale: num(m.scale) };
}

function draftToConfig(d: Draft): MetricConfig {
	const characteristics: Record<string, WeightedModel> = {};
	for (const c of d.characteristics) {
		if (c.name) characteristics[c.name] = modelToConfig(c.model);
	}
	return {
		difficulty: modelToConfig(d.difficulty),
		pp: { multiplier: num(d.pp.multiplier), exponent: num(d.pp.exponent) },
		characteristics,
	};
}

/** Stable signature of a draft — cheap dependency for the scoring effect. */
function draftSignature(d: Draft): string {
	return JSON.stringify(draftToConfig(d));
}

function titleCase(s: string): string {
	return s.charAt(0).toUpperCase() + s.slice(1);
}

// ---------------------------------------------------------------------------

type SelectedMap = { id: string; songName: string; songAuthor: string };

function ScoringLabPage() {
	const { data: configData, isLoading: configLoading } =
		useGetApiAdminMetricsConfig();
	const configResponse =
		configData?.status === 200 ? configData.data : undefined;

	const [draft, setDraft] = useState<Draft | null>(null);
	const [selected, setSelected] = useState<SelectedMap[]>([]);
	const [maps, setMaps] = useState<ScoreMapDto[]>([]);

	// Seed the editor once the server config arrives.
	useEffect(() => {
		if (configResponse) setDraft(toDraft(configResponse.config));
	}, [configResponse]);

	const scoreMutation = usePostApiAdminMetricsScore();
	const { mutate: score } = scoreMutation;

	const runScore = useDebouncedCallback(
		(config: MetricConfig, ids: string[]) => {
			if (ids.length === 0) {
				setMaps([]);
				return;
			}
			score(
				{ data: { config, mapIds: ids } },
				{
					onSuccess: (res) => {
						if (res.status === 200) setMaps(res.data.maps);
					},
				},
			);
		},
		{ wait: SCORE_DEBOUNCE_MS },
	);

	const selectedIds = selected.map((m) => m.id).join(",");
	const signature = draft ? draftSignature(draft) : "";

	// Recompute whenever the config or the selection changes.
	// biome-ignore lint/correctness/useExhaustiveDependencies: signature/selectedIds are the debounced inputs.
	useEffect(() => {
		if (!draft) return;
		runScore(
			draftToConfig(draft),
			selected.map((m) => m.id),
		);
	}, [signature, selectedIds]);

	const catalog = configResponse?.features ?? [];

	const addMap = (m: SelectedMap) =>
		setSelected((prev) =>
			prev.some((x) => x.id === m.id) ? prev : [...prev, m],
		);
	const removeMap = (id: string) =>
		setSelected((prev) => prev.filter((m) => m.id !== id));

	const resetConfig = () => {
		if (configResponse) setDraft(toDraft(configResponse.config));
	};

	return (
		<div className="space-y-6">
			<div className="flex items-center justify-between gap-3">
				<div>
					<h1 className="font-heading text-lg font-semibold tracking-tight">
						Scoring Lab
					</h1>
					<p className="text-sm text-muted-foreground">
						Tune the classification config and watch selected maps re-score
						live. Nothing is saved — export a snippet to apply it.
					</p>
				</div>
				<div className="flex items-center gap-2">
					<Button variant="outline" size="sm" onClick={resetConfig}>
						<RestartIcon className="size-4" />
						Reset
					</Button>
					{draft && <ExportDialog draft={draft} />}
				</div>
			</div>

			<div className="grid gap-6 lg:grid-cols-[minmax(0,420px)_minmax(0,1fr)]">
				{/* Left: config editor */}
				<div className="space-y-4">
					{configLoading || !draft ? (
						<p className="text-sm text-muted-foreground">Loading config…</p>
					) : (
						<ConfigEditor draft={draft} setDraft={setDraft} catalog={catalog} />
					)}
				</div>

				{/* Right: map selection + results */}
				<div className="space-y-4">
					<MapPicker selected={selected} onAdd={addMap} onRemove={removeMap} />
					<ResultsPanel
						maps={maps}
						hasSelection={selected.length > 0}
						pending={scoreMutation.isPending}
					/>
				</div>
			</div>
		</div>
	);
}

// ---------------------------------------------------------------------------
// Config editor
// ---------------------------------------------------------------------------

function ConfigEditor({
	draft,
	setDraft,
	catalog,
}: {
	draft: Draft;
	setDraft: React.Dispatch<React.SetStateAction<Draft | null>>;
	catalog: FeatureCatalogItemDto[];
}) {
	const setDifficulty = (model: DraftModel) =>
		setDraft((prev) => (prev ? { ...prev, difficulty: model } : prev));

	const setCharacteristic = (name: string, model: DraftModel) =>
		setDraft((prev) =>
			prev
				? {
						...prev,
						characteristics: prev.characteristics.map((c) =>
							c.name === name ? { ...c, model } : c,
						),
					}
				: prev,
		);

	const setPp = (patch: Partial<Draft["pp"]>) =>
		setDraft((prev) =>
			prev ? { ...prev, pp: { ...prev.pp, ...patch } } : prev,
		);

	return (
		<>
			<Card>
				<CardHeader>
					<CardTitle className="text-sm">Difficulty</CardTitle>
				</CardHeader>
				<CardContent>
					<ModelEditor
						model={draft.difficulty}
						onChange={setDifficulty}
						catalog={catalog}
					/>
				</CardContent>
			</Card>

			<Card>
				<CardHeader>
					<CardTitle className="text-sm">PP curve</CardTitle>
				</CardHeader>
				<CardContent>
					<p className="mb-3 font-mono text-xs text-muted-foreground">
						pp = multiplier · difficulty^exponent
					</p>
					<div className="grid grid-cols-2 gap-3">
						<NumberField
							label="Multiplier"
							value={draft.pp.multiplier}
							onChange={(v) => setPp({ multiplier: v })}
						/>
						<NumberField
							label="Exponent"
							value={draft.pp.exponent}
							onChange={(v) => setPp({ exponent: v })}
						/>
					</div>
				</CardContent>
			</Card>

			<Card>
				<CardHeader>
					<CardTitle className="text-sm">Characteristics</CardTitle>
				</CardHeader>
				<CardContent>
					{draft.characteristics.length === 0 ? (
						<p className="text-sm text-muted-foreground">
							No characteristics configured.
						</p>
					) : (
						<Tabs defaultValue={draft.characteristics[0].name}>
							<TabsList className="flex-wrap">
								{draft.characteristics.map((c) => (
									<TabsTrigger key={c.name} value={c.name}>
										{titleCase(c.name)}
									</TabsTrigger>
								))}
							</TabsList>
							{draft.characteristics.map((c) => (
								<TabsContent key={c.name} value={c.name} className="mt-4">
									<ModelEditor
										model={c.model}
										onChange={(m) => setCharacteristic(c.name, m)}
										catalog={catalog}
									/>
								</TabsContent>
							))}
						</Tabs>
					)}
				</CardContent>
			</Card>
		</>
	);
}

function ModelEditor({
	model,
	onChange,
	catalog,
}: {
	model: DraftModel;
	onChange: (m: DraftModel) => void;
	catalog: FeatureCatalogItemDto[];
}) {
	const usedKeys = new Set(model.weights.map((w) => w.key));
	const available = catalog.filter((f) => !usedKeys.has(f.key));
	const descriptions = useMemo(
		() => new Map(catalog.map((f) => [f.key, f.description])),
		[catalog],
	);

	const setWeightValue = (key: string, value: string) =>
		onChange({
			...model,
			weights: model.weights.map((w) => (w.key === key ? { ...w, value } : w)),
		});
	const removeWeight = (key: string) =>
		onChange({ ...model, weights: model.weights.filter((w) => w.key !== key) });
	const addWeight = (key: string) => {
		if (!key || usedKeys.has(key)) return;
		onChange({ ...model, weights: [...model.weights, { key, value: "0" }] });
	};

	return (
		<div className="space-y-4">
			<div className="grid grid-cols-2 gap-3">
				<NumberField
					label="Scale"
					value={model.scale}
					onChange={(v) => onChange({ ...model, scale: v })}
				/>
				<NumberField
					label="Bias"
					value={model.bias}
					onChange={(v) => onChange({ ...model, bias: v })}
				/>
			</div>

			<div className="space-y-2">
				<p className="text-xs font-medium text-muted-foreground">Weights</p>
				{model.weights.length === 0 && (
					<p className="text-xs text-muted-foreground/70">No weights.</p>
				)}
				{model.weights.map((w) => (
					<div key={w.key} className="flex items-center gap-2">
						<span
							className="min-w-0 flex-1 truncate font-mono text-xs text-foreground"
							title={descriptions.get(w.key) ?? w.key}
						>
							{w.key}
						</span>
						<Input
							type="text"
							inputMode="decimal"
							value={w.value}
							onChange={(e) => setWeightValue(w.key, e.target.value)}
							className="h-7 w-24 font-mono text-xs tabular-nums"
						/>
						<Button
							type="button"
							variant="ghost"
							size="icon"
							className="size-7 shrink-0 text-muted-foreground"
							onClick={() => removeWeight(w.key)}
							aria-label={`Remove ${w.key}`}
						>
							<CloseCircleIcon className="size-3.5" />
						</Button>
					</div>
				))}
			</div>

			{available.length > 0 && (
				<div className="flex items-center gap-2">
					<AddCircleIcon className="size-3.5 shrink-0 text-muted-foreground" />
					<NativeSelect
						value=""
						onChange={(e) => addWeight(e.target.value)}
						className="h-7 w-full text-xs"
					>
						<NativeSelectOption value="">
							Add feature weight…
						</NativeSelectOption>
						{available.map((f) => (
							<NativeSelectOption key={f.key} value={f.key}>
								{f.key}
							</NativeSelectOption>
						))}
					</NativeSelect>
				</div>
			)}
		</div>
	);
}

function NumberField({
	label,
	value,
	onChange,
}: {
	label: string;
	value: string;
	onChange: (v: string) => void;
}) {
	const id = useId();
	return (
		<div className="flex flex-col gap-1">
			<label htmlFor={id} className="text-xs text-muted-foreground">
				{label}
			</label>
			<Input
				id={id}
				type="text"
				inputMode="decimal"
				value={value}
				onChange={(e) => onChange(e.target.value)}
				className="h-7 font-mono text-xs tabular-nums"
			/>
		</div>
	);
}

// ---------------------------------------------------------------------------
// Map picker
// ---------------------------------------------------------------------------

function MapPicker({
	selected,
	onAdd,
	onRemove,
}: {
	selected: SelectedMap[];
	onAdd: (m: SelectedMap) => void;
	onRemove: (id: string) => void;
}) {
	const [input, setInput] = useState("");
	const [term, setTerm] = useState("");

	const commit = useDebouncedCallback((v: string) => setTerm(v.trim()), {
		wait: SEARCH_DEBOUNCE_MS,
	});

	const { data } = useGetApiMaps(
		{ Page: 1, PageSize: 8, Search: term || undefined },
		{ query: { enabled: term.length > 0 } },
	);
	const results = data?.status === 200 ? data.data.items : [];
	const selectedIds = new Set(selected.map((m) => m.id));

	return (
		<Card>
			<CardHeader>
				<CardTitle className="text-sm">Maps</CardTitle>
			</CardHeader>
			<CardContent className="space-y-3">
				<div className="relative">
					<MagnifierIcon className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
					<Input
						value={input}
						onChange={(e) => {
							setInput(e.target.value);
							commit(e.target.value);
						}}
						placeholder="Search maps to add…"
						className="h-9 pl-8"
					/>
				</div>

				{term.length > 0 && results.length > 0 && (
					<div className="divide-y divide-border overflow-hidden rounded-lg border border-border">
						{results.map((m) => {
							const already = selectedIds.has(m.id);
							return (
								<button
									key={m.id}
									type="button"
									disabled={already}
									onClick={() =>
										onAdd({
											id: m.id,
											songName: m.songName,
											songAuthor: m.songAuthor,
										})
									}
									className={cn(
										"flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-sm transition-colors",
										already ? "cursor-default opacity-50" : "hover:bg-muted/50",
									)}
								>
									<span className="min-w-0 truncate">
										<span className="text-foreground">{m.songName}</span>
										<span className="ml-1.5 text-xs text-muted-foreground">
											{m.songAuthor}
										</span>
									</span>
									{already ? (
										<CheckReadIcon className="size-4 shrink-0 text-muted-foreground" />
									) : (
										<AddCircleIcon className="size-4 shrink-0 text-muted-foreground" />
									)}
								</button>
							);
						})}
					</div>
				)}

				{selected.length === 0 ? (
					<p className="text-xs text-muted-foreground/70">
						No maps selected yet.
					</p>
				) : (
					<div className="flex flex-wrap gap-2">
						{selected.map((m) => (
							<Badge
								key={m.id}
								variant="outline"
								className="gap-1 border-border py-1 pl-2.5 pr-1 text-foreground"
							>
								<span className="max-w-[16rem] truncate">{m.songName}</span>
								<button
									type="button"
									onClick={() => onRemove(m.id)}
									className="rounded-sm p-0.5 text-muted-foreground hover:bg-muted hover:text-foreground"
									aria-label={`Remove ${m.songName}`}
								>
									<CloseCircleIcon className="size-3" />
								</button>
							</Badge>
						))}
					</div>
				)}
			</CardContent>
		</Card>
	);
}

// ---------------------------------------------------------------------------
// Results
// ---------------------------------------------------------------------------

function ResultsPanel({
	maps,
	hasSelection,
	pending,
}: {
	maps: ScoreMapDto[];
	hasSelection: boolean;
	pending: boolean;
}) {
	if (!hasSelection) {
		return (
			<Card>
				<CardContent className="py-10 text-center text-sm text-muted-foreground">
					Select one or more maps to see live scores.
				</CardContent>
			</Card>
		);
	}

	return (
		<div
			className={cn("space-y-4", pending && "opacity-70 transition-opacity")}
		>
			{maps.map((map) => (
				<Card key={map.mapId}>
					<CardHeader>
						<CardTitle className="text-sm">
							{map.songName}
							<span className="ml-2 text-xs font-normal text-muted-foreground">
								{map.songAuthor}
							</span>
						</CardTitle>
					</CardHeader>
					<CardContent className="space-y-3">
						{map.difficulties.length === 0 && (
							<p className="text-xs text-muted-foreground/70">
								No difficulties.
							</p>
						)}
						{map.difficulties.map((d) => (
							<DifficultyResult key={d.difficultyId} d={d} />
						))}
					</CardContent>
				</Card>
			))}
		</div>
	);
}

function DifficultyResult({ d }: { d: ScoreDifficultyDto }) {
	const scored = d.status === "Success";
	const characteristics = Object.entries(d.characteristics);

	return (
		<div className="rounded-lg border border-border p-3">
			<div className="flex items-center gap-2">
				<Badge
					variant="outline"
					className={cn(
						"border",
						RANK_STYLES[d.rank] ??
							"border-border bg-muted text-muted-foreground",
					)}
				>
					{RANK_LABELS[d.rank] ?? d.rank}
				</Badge>
				<span className="text-sm text-foreground">{d.difficultyName}</span>
				{d.characteristic !== "Standard" && (
					<span className="text-xs text-muted-foreground">
						{d.characteristic}
					</span>
				)}
			</div>

			{!scored ? (
				<p className="mt-2 text-xs text-muted-foreground/70">
					{d.status === "FeaturesMissing"
						? "No stored features — analyze this map first."
						: `Not scored (${d.status}).`}
				</p>
			) : (
				<div className="mt-3 grid gap-4 sm:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
					<div className="grid grid-cols-2 gap-3">
						<Headline
							label="Difficulty"
							value={n(d.difficulty)}
							stored={n(d.storedDifficulty)}
							decimals={3}
						/>
						<Headline
							label="PP"
							value={n(d.pp)}
							stored={n(d.storedPp)}
							decimals={0}
						/>
					</div>
					<div className="space-y-2">
						{characteristics.map(([name, value]) => (
							<MetricBar key={name} label={titleCase(name)} value={n(value)} />
						))}
					</div>
				</div>
			)}
		</div>
	);
}

function n(value: number | string | null | undefined): number {
	return value == null ? 0 : Number(value);
}

function Headline({
	label,
	value,
	stored,
	decimals,
}: {
	label: string;
	value: number;
	stored: number;
	decimals: number;
}) {
	const delta = value - stored;
	const hasDelta = Math.abs(delta) > 10 ** -decimals / 2;
	return (
		<div className="rounded-lg border border-border bg-muted/30 p-3">
			<p className="text-xs text-muted-foreground">{label}</p>
			<p className="font-mono text-2xl font-semibold tabular-nums">
				{value.toFixed(decimals)}
			</p>
			{hasDelta && (
				<p
					className={cn(
						"font-mono text-xs tabular-nums",
						delta > 0 ? "text-emerald-400" : "text-rose-400",
					)}
				>
					{delta > 0 ? "+" : ""}
					{delta.toFixed(decimals)} vs stored
				</p>
			)}
		</div>
	);
}

function MetricBar({ label, value }: { label: string; value: number }) {
	const pct = Math.round(Math.max(0, Math.min(1, value)) * 100);
	return (
		<div className="space-y-1">
			<div className="flex items-center justify-between text-xs">
				<span className="text-muted-foreground">{label}</span>
				<span className="font-mono tabular-nums">{value.toFixed(3)}</span>
			</div>
			<div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
				<div
					className="h-full rounded-full bg-primary transition-[width]"
					style={{ width: `${pct}%` }}
				/>
			</div>
		</div>
	);
}

// ---------------------------------------------------------------------------
// Export
// ---------------------------------------------------------------------------

function buildEnvSnippet(d: Draft): string {
	const lines: string[] = [];
	const model = (prefix: string, m: DraftModel) => {
		lines.push(`${prefix}__Scale=${num(m.scale)}`);
		if (num(m.bias) !== 0) lines.push(`${prefix}__Bias=${num(m.bias)}`);
		for (const w of m.weights) {
			if (w.key) lines.push(`${prefix}__Weights__${w.key}=${num(w.value)}`);
		}
	};
	model("Metrics__Difficulty", d.difficulty);
	lines.push(`Metrics__Pp__Multiplier=${num(d.pp.multiplier)}`);
	lines.push(`Metrics__Pp__Exponent=${num(d.pp.exponent)}`);
	for (const c of d.characteristics) {
		if (c.name) model(`Metrics__Characteristics__${c.name}`, c.model);
	}
	return lines.join("\n");
}

function buildJsonSnippet(d: Draft): string {
	const model = (m: DraftModel) => ({
		Weights: Object.fromEntries(
			m.weights.filter((w) => w.key).map((w) => [w.key, num(w.value)]),
		),
		Bias: num(m.bias),
		Scale: num(m.scale),
	});
	const metrics = {
		Difficulty: model(d.difficulty),
		Pp: { Multiplier: num(d.pp.multiplier), Exponent: num(d.pp.exponent) },
		Characteristics: Object.fromEntries(
			d.characteristics
				.filter((c) => c.name)
				.map((c) => [c.name, model(c.model)]),
		),
	};
	return JSON.stringify({ Metrics: metrics }, null, 2);
}

function ExportDialog({ draft }: { draft: Draft }) {
	return (
		<Dialog>
			<DialogTrigger asChild>
				<Button size="sm">
					<CopyIcon className="size-4" />
					Export config
				</Button>
			</DialogTrigger>
			<DialogContent className="max-w-2xl sm:max-w-2xl">
				<DialogHeader>
					<DialogTitle>Export config</DialogTitle>
					<DialogDescription>
						Paste into your environment or appsettings to apply this calibration
						(requires a server restart).
					</DialogDescription>
				</DialogHeader>
				<div className="min-w-0 space-y-4">
					<Snippet title=".env" content={buildEnvSnippet(draft)} />
					<Snippet title="appsettings JSON" content={buildJsonSnippet(draft)} />
				</div>
			</DialogContent>
		</Dialog>
	);
}

function Snippet({ title, content }: { title: string; content: string }) {
	const [copied, setCopied] = useState(false);
	const copy = async () => {
		try {
			await navigator.clipboard.writeText(content);
			setCopied(true);
			setTimeout(() => setCopied(false), 1500);
		} catch {
			// Clipboard unavailable — no-op.
		}
	};
	return (
		<div className="min-w-0">
			<div className="mb-1.5 flex items-center justify-between">
				<span className="text-xs font-medium text-muted-foreground">
					{title}
				</span>
				<Button variant="ghost" size="sm" onClick={copy}>
					{copied ? (
						<CheckReadIcon className="size-3.5" />
					) : (
						<CopyIcon className="size-3.5" />
					)}
					{copied ? "Copied" : "Copy"}
				</Button>
			</div>
			<pre className="max-h-64 w-full max-w-full overflow-auto rounded-lg border border-border bg-muted/30 p-3 font-mono text-xs">
				{content}
			</pre>
		</div>
	);
}

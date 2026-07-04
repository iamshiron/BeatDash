import {
	Card,
	CardContent,
	CardHeader,
	CardTitle,
} from "@shiron/ui/components/ui/card";
import { useMemo } from "react";
import type { NoteItemDto } from "@/api/model";

const COMPONENTS = [
	{
		key: "before",
		label: "Pre-Swing",
		max: 70,
		color: "oklch(0.62 0.19 255)",
		hint: "Backswing arc quality (100° = full)",
	},
	{
		key: "accuracy",
		label: "Accuracy",
		max: 15,
		color: "oklch(0.72 0.19 152)",
		hint: "Cut distance from note center (0.3m = 0)",
	},
	{
		key: "after",
		label: "Post-Swing",
		max: 30,
		color: "oklch(0.75 0.15 65)",
		hint: "Follow-through arc quality (60° = full)",
	},
] as const;

interface HandBreakdown {
	before: number;
	accuracy: number;
	after: number;
	count: number;
}

function computeBreakdown(notes: NoteItemDto[]): HandBreakdown {
	if (notes.length === 0) {
		return { before: 0, accuracy: 0, after: 0, count: 0 };
	}
	let before = 0;
	let accuracy = 0;
	let after = 0;
	for (const n of notes) {
		before += Number(n.beforeCutScore);
		accuracy += Number(n.centerDistanceScore);
		after += Number(n.afterCutScore);
	}
	return {
		before: before / notes.length,
		accuracy: accuracy / notes.length,
		after: after / notes.length,
		count: notes.length,
	};
}

export function ScoreBreakdown({ notes }: { notes: NoteItemDto[] }) {
	const { overall, left, right } = useMemo(() => {
		const leftNotes = notes.filter((n) => Number(n.colorType) === 0);
		const rightNotes = notes.filter((n) => Number(n.colorType) === 1);
		return {
			overall: computeBreakdown(notes),
			left: computeBreakdown(leftNotes),
			right: computeBreakdown(rightNotes),
		};
	}, [notes]);

	if (overall.count === 0) return null;

	const totalAvg = overall.before + overall.accuracy + overall.after;

	return (
		<Card className="mb-4">
			<CardHeader>
				<CardTitle>Score Breakdown</CardTitle>
			</CardHeader>
			<CardContent>
				<div className="mb-4 flex items-baseline gap-2">
					<span className="font-heading text-4xl font-bold tracking-tight">
						{totalAvg.toFixed(1)}
					</span>
					<span className="text-base text-muted-foreground">
						/ 115 avg per note
					</span>
				</div>

				<div className="flex h-2.5 overflow-hidden rounded-full bg-muted">
					{COMPONENTS.map((c) => {
						const val = overall[c.key];
						const pct = (val / 115) * 100;
						return (
							<div
								key={c.key}
								className="h-full transition-all"
								style={{
									width: `${pct}%`,
									backgroundColor: c.color,
								}}
							/>
						);
					})}
				</div>

				<div className="mt-4 space-y-3">
					{COMPONENTS.map((c) => {
						const val = overall[c.key];
						const leftVal = left[c.key];
						const rightVal = right[c.key];
						const pct = (val / c.max) * 100;
						return (
							<div key={c.key}>
								<div className="mb-1 flex items-baseline justify-between">
									<div>
										<span className="text-sm font-medium">{c.label}</span>
										<span className="ml-2 text-xs text-muted-foreground">
											{c.hint}
										</span>
									</div>
									<span className="font-heading text-xl font-semibold tabular-nums">
										{val.toFixed(1)}
										<span className="text-sm font-normal text-muted-foreground">
											/{c.max}
										</span>
									</span>
								</div>
								<div className="flex h-6 items-center gap-2">
									<div className="relative h-2 flex-1 overflow-hidden rounded-full bg-muted">
										<div
											className="h-full rounded-full transition-all"
											style={{
												width: `${pct}%`,
												backgroundColor: c.color,
											}}
										/>
									</div>
									{left.count > 0 && right.count > 0 && (
										<div className="flex shrink-0 gap-2 font-mono text-[10px] tabular-nums text-muted-foreground">
											<span className="text-rose-400">
												L {leftVal.toFixed(1)}
											</span>
											<span className="text-sky-400">
												R {rightVal.toFixed(1)}
											</span>
										</div>
									)}
								</div>
							</div>
						);
					})}
				</div>
			</CardContent>
		</Card>
	);
}

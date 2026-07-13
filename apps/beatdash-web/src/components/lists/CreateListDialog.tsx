import { Button } from "@shiron/ui/components/ui/button";
import {
	Dialog,
	DialogContent,
	DialogDescription,
	DialogFooter,
	DialogHeader,
	DialogTitle,
} from "@shiron/ui/components/ui/dialog";
import { Field, FieldGroup, FieldLabel } from "@shiron/ui/components/ui/field";
import { Input } from "@shiron/ui/components/ui/input";
import { Textarea } from "@shiron/ui/components/ui/textarea";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { getGetApiListsQueryKey, usePostApiLists } from "@/api/lists/lists";

type CreateListDialogProps = {
	open: boolean;
	onOpenChange: (open: boolean) => void;
	/** Called with the new list's id after a successful create. */
	onCreated?: (listId: string) => void;
};

const NAME_MAX = 64;
const DESCRIPTION_MAX = 512;

export function CreateListDialog({
	open,
	onOpenChange,
	onCreated,
}: CreateListDialogProps) {
	const queryClient = useQueryClient();
	const [name, setName] = useState("");
	const [description, setDescription] = useState("");

	const reset = () => {
		setName("");
		setDescription("");
	};

	const createMutation = usePostApiLists({
		mutation: {
			onSuccess: async (res) => {
				if (res.status !== 201) {
					toast.error("Couldn't create the list.");
					return;
				}
				await queryClient.invalidateQueries({
					queryKey: getGetApiListsQueryKey(),
				});
				toast.success(`Created "${res.data.name}".`);
				onOpenChange(false);
				onCreated?.(res.data.id);
				reset();
			},
			onError: () => toast.error("Couldn't create the list."),
		},
	});

	const trimmed = name.trim();
	const canSubmit = trimmed.length > 0 && !createMutation.isPending;

	const submit = () => {
		if (!canSubmit) return;
		createMutation.mutate({
			data: {
				name: trimmed,
				description: description.trim() || null,
			},
		});
	};

	return (
		<Dialog
			open={open}
			onOpenChange={(next) => {
				if (!next) reset();
				onOpenChange(next);
			}}
		>
			<DialogContent>
				<DialogHeader>
					<DialogTitle>New list</DialogTitle>
					<DialogDescription>
						Group maps together — like "Warmup" or "Trying to Beat".
					</DialogDescription>
				</DialogHeader>

				<FieldGroup>
					<Field>
						<FieldLabel htmlFor="list-name">Name</FieldLabel>
						<Input
							id="list-name"
							value={name}
							maxLength={NAME_MAX}
							autoFocus
							placeholder="Warmup"
							onChange={(e) => setName(e.target.value)}
							onKeyDown={(e) => {
								if (e.key === "Enter") submit();
							}}
						/>
					</Field>
					<Field>
						<FieldLabel htmlFor="list-description">
							Description{" "}
							<span className="text-muted-foreground">(optional)</span>
						</FieldLabel>
						<Textarea
							id="list-description"
							value={description}
							maxLength={DESCRIPTION_MAX}
							rows={3}
							placeholder="What's this list for?"
							onChange={(e) => setDescription(e.target.value)}
						/>
					</Field>
				</FieldGroup>

				<DialogFooter>
					<Button
						variant="ghost"
						onClick={() => onOpenChange(false)}
						disabled={createMutation.isPending}
					>
						Cancel
					</Button>
					<Button onClick={submit} disabled={!canSubmit}>
						Create
					</Button>
				</DialogFooter>
			</DialogContent>
		</Dialog>
	);
}

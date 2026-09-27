import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useBoard, useDeleteCard, useUpdateCard } from "../hooks/useBoard";
import { ApiError } from "../services/apiClient";
import { CARD_TYPE_OPTIONS, PRIORITY_OPTIONS } from "../types/options";
import { PRIORITY_STYLES, TYPE_LABELS } from "./CardItem";

interface CardDetailModalProps {
  cardId: string;
  appId: string;
  onClose: () => void;
}

const updateCardSchema = z.object({
  title: z.string().min(1, "Title is required"),
  description: z.string().optional(),
  cardType: z.enum(["feature", "bug", "chore", "idea"]),
  priority: z.enum(["low", "medium", "high", "critical"]),
});

type UpdateCardFormValues = z.infer<typeof updateCardSchema>;

export function CardDetailModal({ cardId, appId, onClose }: CardDetailModalProps) {
  const { data: board } = useBoard(appId);
  const updateCardMutation = useUpdateCard(appId);
  const deleteCardMutation = useDeleteCard(appId);

  const [isEditing, setIsEditing] = useState(false);
  const [isConfirmingDelete, setIsConfirmingDelete] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);

  const column = board?.columns.find((candidate) => candidate.cards.some((card) => card.id === cardId));
  const card = column?.cards.find((candidate) => candidate.id === cardId);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<UpdateCardFormValues>({
    resolver: zodResolver(updateCardSchema),
    defaultValues: {
      title: card?.title ?? "",
      description: card?.description ?? "",
      cardType: card?.cardType ?? "feature",
      priority: card?.priority ?? "medium",
    },
  });

  useEffect(() => {
    if (card) {
      reset({
        title: card.title,
        description: card.description ?? "",
        cardType: card.cardType,
        priority: card.priority,
      });
    }
  }, [card, reset]);

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onClose]);

  if (!card || !column) {
    return null;
  }

  const onSubmit = (values: UpdateCardFormValues) => {
    setServerError(null);
    updateCardMutation.mutate(
      { cardId, payload: values },
      {
        onSuccess: () => {
          setIsEditing(false);
        },
        onError: (error) => {
          setServerError(error instanceof ApiError ? error.message : "Failed to update card.");
        },
      },
    );
  };

  const handleDelete = () => {
    setServerError(null);
    deleteCardMutation.mutate(cardId, {
      onSuccess: () => {
        onClose();
      },
      onError: (error) => {
        setServerError(error instanceof ApiError ? error.message : "Failed to delete card.");
        setIsConfirmingDelete(false);
      },
    });
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 px-4"
      onClick={onClose}
      role="presentation"
    >
      <div
        className="w-full max-w-lg rounded-card border border-white/10 bg-gray-950/80 p-6 shadow-2xl backdrop-blur-md"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-white">{isEditing ? "Edit card" : "Card details"}</h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md px-2 py-1 text-sm text-gray-400 hover:bg-gray-800 hover:text-white"
            aria-label="Close"
          >
            ✕
          </button>
        </div>

        {serverError ? (
          <div className="mb-3 rounded-lg border border-red-800 bg-red-950 px-3 py-2 text-sm text-red-300">
            {serverError}
          </div>
        ) : null}

        {isEditing ? (
          <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-3" noValidate>
            <div className="flex flex-col gap-1">
              <label htmlFor="edit-card-title" className="text-sm font-medium text-gray-300">
                Title
              </label>
              <input
                id="edit-card-title"
                type="text"
                className="rounded-lg border border-gray-700 bg-gray-800 px-3 py-2 text-sm text-white outline-none focus:border-brand-focus"
                {...register("title")}
              />
              {errors.title ? <p className="text-xs text-red-400">{errors.title.message}</p> : null}
            </div>

            <div className="flex flex-col gap-1">
              <label htmlFor="edit-card-description" className="text-sm font-medium text-gray-300">
                Description
              </label>
              <textarea
                id="edit-card-description"
                rows={4}
                className="rounded-lg border border-gray-700 bg-gray-800 px-3 py-2 text-sm text-white outline-none focus:border-brand-focus"
                {...register("description")}
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-1">
                <label htmlFor="edit-card-type" className="text-sm font-medium text-gray-300">
                  Type
                </label>
                <select
                  id="edit-card-type"
                  className="rounded-lg border border-gray-700 bg-gray-800 px-3 py-2 text-sm text-white outline-none focus:border-brand-focus"
                  {...register("cardType")}
                >
                  {CARD_TYPE_OPTIONS.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex flex-col gap-1">
                <label htmlFor="edit-card-priority" className="text-sm font-medium text-gray-300">
                  Priority
                </label>
                <select
                  id="edit-card-priority"
                  className="rounded-lg border border-gray-700 bg-gray-800 px-3 py-2 text-sm text-white outline-none focus:border-brand-focus"
                  {...register("priority")}
                >
                  {PRIORITY_OPTIONS.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="mt-2 flex items-center gap-2">
              <button
                type="submit"
                disabled={isSubmitting || updateCardMutation.isPending}
                className="rounded-lg bg-brand px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-brand-dark disabled:cursor-not-allowed disabled:opacity-60"
              >
                {updateCardMutation.isPending ? "Saving..." : "Save changes"}
              </button>
              <button
                type="button"
                onClick={() => {
                  setIsEditing(false);
                  reset({
                    title: card.title,
                    description: card.description ?? "",
                    cardType: card.cardType,
                    priority: card.priority,
                  });
                }}
                className="rounded-lg border border-white/10 px-4 py-2 text-sm text-gray-300 hover:border-white/40 hover:text-white"
              >
                Cancel
              </button>
            </div>
          </form>
        ) : (
          <div className="flex flex-col gap-3">
            <div>
              <h3 className="text-base font-semibold text-white">{card.title}</h3>
              <p className="mt-1 whitespace-pre-wrap text-sm text-gray-300">
                {card.description ? card.description : "No description"}
              </p>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <span className="rounded-full bg-gray-800 px-2 py-0.5 text-[11px] text-gray-300">
                {TYPE_LABELS[card.cardType]}
              </span>
              <span className={`rounded-full px-2 py-0.5 text-[11px] ${PRIORITY_STYLES[card.priority]}`}>
                {card.priority}
              </span>
              <span className="rounded-full bg-gray-800 px-2 py-0.5 text-[11px] text-gray-300">
                {column.name}
              </span>
            </div>

            <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-xs text-gray-400">
              <div>
                <dt className="text-gray-500">Linked branch</dt>
                <dd className="text-gray-300">{card.linkedBranch ? card.linkedBranch : "No branch linked"}</dd>
              </div>
              <div>
                <dt className="text-gray-500">Column</dt>
                <dd className="text-gray-300">{column.name}</dd>
              </div>
              <div>
                <dt className="text-gray-500">Created</dt>
                <dd className="text-gray-300">{new Date(card.createdAt).toLocaleString()}</dd>
              </div>
              <div>
                <dt className="text-gray-500">Updated</dt>
                <dd className="text-gray-300">{new Date(card.updatedAt).toLocaleString()}</dd>
              </div>
            </dl>

            <div className="mt-2 flex items-center gap-2">
              <button
                type="button"
                onClick={() => setIsEditing(true)}
                className="rounded-lg bg-brand px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-brand-dark"
              >
                Edit
              </button>

              {isConfirmingDelete ? (
                <div className="flex items-center gap-2 rounded-lg border border-red-800 bg-red-950 px-3 py-2 text-sm text-red-300">
                  <span>Delete this card?</span>
                  <button
                    type="button"
                    onClick={handleDelete}
                    disabled={deleteCardMutation.isPending}
                    className="rounded-md bg-red-800 px-2 py-1 text-xs font-semibold text-white hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    {deleteCardMutation.isPending ? "Deleting..." : "Confirm"}
                  </button>
                  <button
                    type="button"
                    onClick={() => setIsConfirmingDelete(false)}
                    className="rounded-md px-2 py-1 text-xs text-red-300 hover:text-white"
                  >
                    Cancel
                  </button>
                </div>
              ) : (
                <button
                  type="button"
                  onClick={() => setIsConfirmingDelete(true)}
                  className="rounded-lg border border-red-800 px-4 py-2 text-sm text-red-300 hover:bg-red-950"
                >
                  Delete
                </button>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

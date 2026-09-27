import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useApps } from "../hooks/useApps";
import { boardQueryKey, useBoard } from "../hooks/useBoard";
import { cardsApi } from "../services/api";
import { ApiError } from "../services/apiClient";
import { useUiStore } from "../stores/uiStore";
import { CARD_TYPE_OPTIONS, PRIORITY_OPTIONS } from "../types/options";

const newCardSchema = z.object({
  appId: z.string().min(1, "Choose an app"),
  title: z.string().min(1, "Title is required"),
  description: z.string().optional(),
  cardType: z.enum(["feature", "bug", "chore", "idea"]),
  priority: z.enum(["low", "medium", "high", "critical"]),
});

type NewCardFormValues = z.infer<typeof newCardSchema>;

export function NewCardModal() {
  const isOpen = useUiStore((state) => state.isNewCardModalOpen);
  const defaults = useUiStore((state) => state.newCardDefaults);
  const closeModal = useUiStore((state) => state.closeNewCardModal);
  const { data: apps } = useApps();
  const queryClient = useQueryClient();
  const [serverError, setServerError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<NewCardFormValues>({
    resolver: zodResolver(newCardSchema),
    defaultValues: {
      appId: defaults.appId ?? "",
      title: "",
      description: "",
      cardType: "feature",
      priority: "medium",
    },
  });

  const selectedAppId = watch("appId");
  const { data: selectedAppBoard } = useBoard(selectedAppId || undefined);
  const fallbackColumnId = defaults.columnId ?? [...(selectedAppBoard?.columns ?? [])].sort((a, b) => a.order - b.order)[0]?.id;

  useEffect(() => {
    if (isOpen) {
      reset({
        appId: defaults.appId ?? "",
        title: "",
        description: "",
        cardType: "feature",
        priority: "medium",
      });
      setServerError(null);
    }
  }, [isOpen, defaults.appId, reset]);

  const createCardMutation = useMutation({
    mutationFn: (values: NewCardFormValues) => {
      const boardId = selectedAppBoard?.id;
      if (!boardId || !fallbackColumnId) {
        throw new ApiError(400, null, "This app's board hasn't loaded yet. Select the app and try again.");
      }
      return cardsApi.create({
        boardId,
        columnId: fallbackColumnId,
        title: values.title,
        description: values.description,
        cardType: values.cardType,
        priority: values.priority,
      });
    },
    onSuccess: (_data, variables) => {
      void queryClient.invalidateQueries({ queryKey: boardQueryKey(variables.appId) });
      closeModal();
    },
    onError: (error) => {
      setServerError(error instanceof ApiError ? error.message : "Failed to create card.");
    },
  });

  if (!isOpen) {
    return null;
  }

  const onSubmit = (values: NewCardFormValues) => {
    setServerError(null);
    createCardMutation.mutate(values);
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 px-4"
      onClick={closeModal}
      role="presentation"
    >
      <div
        className="w-full max-w-md rounded-card border border-gray-800 bg-gray-900 p-6 shadow-2xl"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-white">New card</h2>
          <button
            type="button"
            onClick={closeModal}
            className="rounded-md px-2 py-1 text-sm text-gray-400 hover:bg-gray-800 hover:text-white"
            aria-label="Close"
          >
            ✕
          </button>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-3" noValidate>
          <div className="flex flex-col gap-1">
            <label htmlFor="new-card-app" className="text-sm font-medium text-gray-300">
              App
            </label>
            <select
              id="new-card-app"
              className="rounded-lg border border-gray-700 bg-gray-800 px-3 py-2 text-sm text-white outline-none focus:border-brand"
              {...register("appId")}
            >
              <option value="">Select an app</option>
              {apps?.map((app) => (
                <option key={app.id} value={app.id}>
                  {app.name}
                </option>
              ))}
            </select>
            {errors.appId ? <p className="text-xs text-red-400">{errors.appId.message}</p> : null}
          </div>

          <div className="flex flex-col gap-1">
            <label htmlFor="new-card-title" className="text-sm font-medium text-gray-300">
              Title
            </label>
            <input
              id="new-card-title"
              type="text"
              className="rounded-lg border border-gray-700 bg-gray-800 px-3 py-2 text-sm text-white outline-none focus:border-brand"
              {...register("title")}
            />
            {errors.title ? <p className="text-xs text-red-400">{errors.title.message}</p> : null}
          </div>

          <div className="flex flex-col gap-1">
            <label htmlFor="new-card-description" className="text-sm font-medium text-gray-300">
              Description
            </label>
            <textarea
              id="new-card-description"
              rows={3}
              className="rounded-lg border border-gray-700 bg-gray-800 px-3 py-2 text-sm text-white outline-none focus:border-brand"
              {...register("description")}
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1">
              <label htmlFor="new-card-type" className="text-sm font-medium text-gray-300">
                Type
              </label>
              <select
                id="new-card-type"
                className="rounded-lg border border-gray-700 bg-gray-800 px-3 py-2 text-sm text-white outline-none focus:border-brand"
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
              <label htmlFor="new-card-priority" className="text-sm font-medium text-gray-300">
                Priority
              </label>
              <select
                id="new-card-priority"
                className="rounded-lg border border-gray-700 bg-gray-800 px-3 py-2 text-sm text-white outline-none focus:border-brand"
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

          {!defaults.columnId && selectedAppId ? (
            <p className="rounded-lg border border-amber-800 bg-amber-950 px-3 py-2 text-xs text-amber-300">
              This card will be added to the app's first column (Backlog). Open it from the board to target a
              different column.
            </p>
          ) : null}

          {serverError ? (
            <div className="rounded-lg border border-red-800 bg-red-950 px-3 py-2 text-sm text-red-300">
              {serverError}
            </div>
          ) : null}

          <button
            type="submit"
            disabled={isSubmitting || createCardMutation.isPending}
            className="mt-2 rounded-lg bg-brand px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-brand-dark disabled:cursor-not-allowed disabled:opacity-60"
          >
            {createCardMutation.isPending ? "Creating..." : "Create card"}
          </button>
        </form>
      </div>
    </div>
  );
}

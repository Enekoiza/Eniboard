import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useCreateApp } from "../hooks/useApps";
import { ApiError } from "../services/apiClient";

const newAppSchema = z.object({
  name: z.string().min(1, "Name is required"),
  color: z.string().min(1, "Pick a color"),
  repoUrl: z.union([z.literal(""), z.string().url("Must be a valid URL")]).optional(),
});

type NewAppFormValues = z.infer<typeof newAppSchema>;

interface NewAppModalProps {
  onClose: () => void;
}

export function NewAppModal({ onClose }: NewAppModalProps) {
  const createAppMutation = useCreateApp();
  const [serverError, setServerError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<NewAppFormValues>({
    resolver: zodResolver(newAppSchema),
    defaultValues: { name: "", color: "#6366f1", repoUrl: "" },
  });

  const onSubmit = async (values: NewAppFormValues) => {
    setServerError(null);
    try {
      await createAppMutation.mutateAsync({
        name: values.name,
        color: values.color,
        repoUrl: values.repoUrl || undefined,
      });
      onClose();
    } catch (error) {
      setServerError(error instanceof ApiError ? error.message : "Failed to create app.");
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 px-4"
      onClick={onClose}
      role="presentation"
    >
      <div
        className="w-full max-w-md rounded-card border border-white/10 bg-gray-950/80 p-6 shadow-2xl backdrop-blur-md"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-white">New app</h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md px-2 py-1 text-sm text-gray-400 hover:bg-gray-800 hover:text-white"
            aria-label="Close"
          >
            ✕
          </button>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-3" noValidate>
          <div className="flex flex-col gap-1">
            <label htmlFor="new-app-name" className="text-sm font-medium text-gray-300">
              Name
            </label>
            <input
              id="new-app-name"
              type="text"
              className="rounded-lg border border-gray-700 bg-gray-800 px-3 py-2 text-sm text-white outline-none focus:border-brand-focus"
              {...register("name")}
            />
            {errors.name ? <p className="text-xs text-red-400">{errors.name.message}</p> : null}
          </div>

          <div className="flex flex-col gap-1">
            <label htmlFor="new-app-color" className="text-sm font-medium text-gray-300">
              Color
            </label>
            <input
              id="new-app-color"
              type="color"
              className="h-10 w-16 cursor-pointer rounded-lg border border-gray-700 bg-gray-800 p-1"
              {...register("color")}
            />
            {errors.color ? <p className="text-xs text-red-400">{errors.color.message}</p> : null}
          </div>

          <div className="flex flex-col gap-1">
            <label htmlFor="new-app-repo" className="text-sm font-medium text-gray-300">
              Repo URL (optional)
            </label>
            <input
              id="new-app-repo"
              type="text"
              placeholder="https://github.com/you/repo"
              className="rounded-lg border border-gray-700 bg-gray-800 px-3 py-2 text-sm text-white outline-none focus:border-brand-focus"
              {...register("repoUrl")}
            />
            {errors.repoUrl ? <p className="text-xs text-red-400">{errors.repoUrl.message}</p> : null}
          </div>

          {serverError ? (
            <div className="rounded-lg border border-red-800 bg-red-950 px-3 py-2 text-sm text-red-300">
              {serverError}
            </div>
          ) : null}

          <button
            type="submit"
            disabled={isSubmitting || createAppMutation.isPending}
            className="mt-2 rounded-lg bg-brand px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-brand-dark disabled:cursor-not-allowed disabled:opacity-60"
          >
            {createAppMutation.isPending ? "Creating..." : "Create app"}
          </button>
        </form>
      </div>
    </div>
  );
}

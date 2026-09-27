import { useState } from "react";
import { Link } from "react-router-dom";
import { useApps } from "../hooks/useApps";
import { NewAppModal } from "../components/NewAppModal";

export function DashboardPage() {
  const { data: apps, isLoading, isError, error } = useApps();
  const [isNewAppModalOpen, setIsNewAppModalOpen] = useState(false);

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-white">Your apps</h1>
        <button
          type="button"
          onClick={() => setIsNewAppModalOpen(true)}
          className="rounded-lg bg-brand px-4 py-2 text-sm font-semibold text-white hover:bg-brand-dark"
        >
          New app
        </button>
      </div>

      {isLoading ? <p className="text-sm text-gray-400">Loading apps...</p> : null}
      {isError ? (
        <p className="text-sm text-red-400">
          Failed to load apps{error instanceof Error ? `: ${error.message}` : ""}.
        </p>
      ) : null}

      {!isLoading && !isError && apps?.length === 0 ? (
        <div className="rounded-card border border-dashed border-white/10 bg-gray-950/40 p-10 text-center text-gray-400 backdrop-blur-sm">
          No apps yet. Create your first app to get its board set up automatically.
        </div>
      ) : null}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {apps?.map((app) => (
          <Link
            key={app.id}
            to={`/apps/${app.id}`}
            className="flex flex-col gap-3 rounded-card border border-white/10 bg-gray-950/70 p-5 backdrop-blur-sm transition-colors hover:border-brand"
          >
            <div className="flex items-center gap-3">
              <span
                className="h-4 w-4 shrink-0 rounded-full border border-white/10"
                style={{ backgroundColor: app.color }}
                aria-hidden="true"
              />
              <h2 className="truncate text-base font-semibold text-white">{app.name}</h2>
            </div>
            {app.repoUrl ? <p className="truncate text-xs text-gray-500">{app.repoUrl}</p> : null}
            <div className="mt-2 flex gap-4 text-xs text-gray-500">
              <span>Board ready</span>
            </div>
          </Link>
        ))}
      </div>

      {isNewAppModalOpen ? <NewAppModal onClose={() => setIsNewAppModalOpen(false)} /> : null}
    </div>
  );
}

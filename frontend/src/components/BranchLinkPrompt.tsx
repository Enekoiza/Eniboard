import { useState } from "react";

interface BranchLinkPromptProps {
  cardTitle: string;
  onConfirm: (linkedBranch: string | undefined) => void;
  onCancel: () => void;
}

export function BranchLinkPrompt({ cardTitle, onConfirm, onCancel }: BranchLinkPromptProps) {
  const [branch, setBranch] = useState("");

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 px-4"
      onClick={onCancel}
      role="presentation"
    >
      <div
        className="w-full max-w-sm rounded-card border border-white/10 bg-gray-950/80 p-6 shadow-2xl backdrop-blur-md"
        onClick={(event) => event.stopPropagation()}
      >
        <h2 className="mb-1 text-lg font-semibold text-white">Move to Doing</h2>
        <p className="mb-4 text-sm text-gray-400">
          Optionally link a branch for <span className="text-gray-200">{cardTitle}</span>.
        </p>
        <input
          type="text"
          autoFocus
          placeholder="feature/my-branch"
          value={branch}
          onChange={(event) => setBranch(event.target.value)}
          className="mb-4 w-full rounded-lg border border-gray-700 bg-gray-800 px-3 py-2 text-sm text-white outline-none focus:border-brand-focus"
        />
        <div className="flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            className="rounded-lg border border-gray-700 px-3 py-1.5 text-sm text-gray-300 hover:bg-gray-800"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={() => onConfirm(branch.trim() || undefined)}
            className="rounded-lg bg-brand px-3 py-1.5 text-sm font-semibold text-white hover:bg-brand-dark"
          >
            Confirm move
          </button>
        </div>
      </div>
    </div>
  );
}

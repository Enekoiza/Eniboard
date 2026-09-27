import { useDroppable } from "@dnd-kit/core";
import type { BoardColumn } from "../types";
import { CardItem } from "./CardItem";
import { useUiStore } from "../stores/uiStore";

interface BoardColumnViewProps {
  column: BoardColumn;
  appId: string;
}

export function BoardColumnView({ column, appId }: BoardColumnViewProps) {
  const { setNodeRef, isOver } = useDroppable({ id: column.id });
  const openNewCardModal = useUiStore((state) => state.openNewCardModal);
  const isAtLimit = column.wipLimit != null && column.cards.length >= column.wipLimit;

  return (
    <div className="flex w-72 shrink-0 flex-col gap-3">
      <div className="flex items-center justify-between px-1">
        <h2 className="text-sm font-semibold text-gray-200">{column.name}</h2>
        <span
          className={`rounded-full px-2 py-0.5 text-xs ${
            isAtLimit ? "bg-red-900 text-red-300" : "bg-gray-800 text-gray-400"
          }`}
        >
          {column.cards.length}
          {column.wipLimit != null ? ` / ${column.wipLimit}` : ""}
        </span>
      </div>

      <div
        ref={setNodeRef}
        className={`flex min-h-[120px] flex-1 flex-col gap-2 rounded-card border border-dashed p-2 transition-colors ${
          isOver ? "border-brand bg-brand/5" : "border-gray-800"
        }`}
      >
        {column.cards.map((card) => (
          <CardItem key={card.id} card={card} />
        ))}
        {column.cards.length === 0 ? (
          <p className="px-2 py-4 text-center text-xs text-gray-600">Drop cards here</p>
        ) : null}
      </div>

      <button
        type="button"
        onClick={() => openNewCardModal({ appId, columnId: column.id })}
        className="rounded-lg border border-gray-800 px-2 py-1.5 text-xs text-gray-400 hover:border-brand hover:text-white"
      >
        + Add card
      </button>
    </div>
  );
}

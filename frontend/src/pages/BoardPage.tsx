import { useEffect, useState } from "react";
import { DndContext, KeyboardSensor, PointerSensor, useSensor, useSensors } from "@dnd-kit/core";
import type { DragEndEvent } from "@dnd-kit/core";
import { Link, useParams } from "react-router-dom";
import { useApps } from "../hooks/useApps";
import { useBoard, useMoveCard } from "../hooks/useBoard";
import { BoardColumnView } from "../components/BoardColumnView";
import { BranchLinkPrompt } from "../components/BranchLinkPrompt";
import { CardDetailModal } from "../components/CardDetailModal";
import { ApiError } from "../services/apiClient";
import { useUiStore } from "../stores/uiStore";

const DOING_COLUMN_NAME = "Doing";

interface PendingMove {
  cardId: string;
  cardTitle: string;
  targetColumnId: string;
}

export function BoardPage() {
  const { appId } = useParams<{ appId: string }>();
  const { data: apps } = useApps();
  const { data: board, isLoading, isError, error } = useBoard(appId);
  const moveCardMutation = useMoveCard(appId);
  const [blockedMessage, setBlockedMessage] = useState<string | null>(null);
  const [pendingMove, setPendingMove] = useState<PendingMove | null>(null);
  const [selectedCardId, setSelectedCardId] = useState<string | null>(null);
  const setDragActive = useUiStore((state) => state.setDragActive);

  useEffect(() => () => setDragActive(false), [setDragActive]);

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor),
  );

  const app = apps?.find((candidate) => candidate.id === appId);

  const executeMove = (cardId: string, targetColumnId: string, linkedBranch?: string) => {
    moveCardMutation.mutate(
      { cardId, payload: { targetColumnId, linkedBranch } },
      {
        onError: (moveError) => {
          if (moveError instanceof ApiError && moveError.status === 409) {
            setBlockedMessage(
              moveError.problem?.detail ??
                "That column is at its WIP limit. Move the existing card out of Doing first.",
            );
          } else {
            setBlockedMessage(moveError instanceof ApiError ? moveError.message : "Failed to move card.");
          }
        },
      },
    );
  };

  const handleDragEnd = (event: DragEndEvent) => {
    setDragActive(false);
    const { active, over } = event;
    if (!over || !board) return;

    const cardId = String(active.id);
    const targetColumnId = String(over.id);

    const sourceColumn = board.columns.find((column) => column.cards.some((card) => card.id === cardId));
    const targetColumn = board.columns.find((column) => column.id === targetColumnId);
    if (!sourceColumn || !targetColumn || sourceColumn.id === targetColumn.id) {
      return;
    }

    const card = sourceColumn.cards.find((candidate) => candidate.id === cardId);
    if (!card) return;

    if (targetColumn.wipLimit != null && targetColumn.cards.length >= targetColumn.wipLimit) {
      setBlockedMessage(`Move the existing card out of ${targetColumn.name} first.`);
      return;
    }

    if (targetColumn.name === DOING_COLUMN_NAME) {
      setPendingMove({ cardId, cardTitle: card.title, targetColumnId });
      return;
    }

    executeMove(cardId, targetColumnId);
  };

  if (isLoading) {
    return <p className="inline-block rounded-lg bg-gray-950/70 px-3 py-2 text-sm text-gray-300">Loading board...</p>;
  }

  if (isError || !board) {
    return (
      <p className="inline-block rounded-lg bg-gray-950/70 px-3 py-2 text-sm text-red-300">
        Failed to load board{error instanceof Error ? `: ${error.message}` : ""}.
      </p>
    );
  }

  const orderedColumns = [...board.columns].sort((a, b) => a.order - b.order);

  return (
    <div>
      <div className="mb-6 flex items-center gap-3 rounded-card bg-gray-950/70 px-4 py-3">
        <Link to="/" className="text-sm text-gray-300 hover:text-white">
          ← Apps
        </Link>
        {app ? (
          <div className="flex items-center gap-2">
            <span className="h-3 w-3 rounded-full" style={{ backgroundColor: app.color }} aria-hidden="true" />
            <h1 className="text-xl font-semibold text-white">{app.name}</h1>
          </div>
        ) : null}
      </div>

      {blockedMessage ? (
        <div className="mb-4 flex items-center justify-between rounded-lg border border-red-800 bg-red-950 px-4 py-2 text-sm text-red-300">
          <span>{blockedMessage}</span>
          <button
            type="button"
            onClick={() => setBlockedMessage(null)}
            className="ml-4 text-red-400 hover:text-white"
            aria-label="Dismiss"
          >
            ✕
          </button>
        </div>
      ) : null}

      <DndContext
        sensors={sensors}
        onDragStart={() => setDragActive(true)}
        onDragEnd={handleDragEnd}
        onDragCancel={() => setDragActive(false)}
      >
        <div className="flex flex-col gap-4 pb-4 md:flex-row">
          {orderedColumns.map((column) => (
            <BoardColumnView
              key={column.id}
              column={column}
              appId={appId as string}
              onOpen={setSelectedCardId}
            />
          ))}
        </div>
      </DndContext>

      {selectedCardId ? (
        <CardDetailModal
          cardId={selectedCardId}
          appId={appId as string}
          onClose={() => setSelectedCardId(null)}
        />
      ) : null}

      {pendingMove ? (
        <BranchLinkPrompt
          cardTitle={pendingMove.cardTitle}
          onCancel={() => setPendingMove(null)}
          onConfirm={(linkedBranch) => {
            executeMove(pendingMove.cardId, pendingMove.targetColumnId, linkedBranch);
            setPendingMove(null);
          }}
        />
      ) : null}
    </div>
  );
}

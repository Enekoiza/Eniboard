import { useDraggable } from "@dnd-kit/core";
import type { Card } from "../types";

interface CardItemProps {
  card: Card;
  onOpen: (cardId: string) => void;
}

export const PRIORITY_STYLES: Record<Card["priority"], string> = {
  low: "bg-gray-700 text-gray-200",
  medium: "bg-sky-800 text-sky-200",
  high: "bg-amber-800 text-amber-200",
  critical: "bg-red-800 text-red-200",
};

export const TYPE_LABELS: Record<Card["cardType"], string> = {
  feature: "Feature",
  bug: "Bug",
  chore: "Chore",
  idea: "Idea",
};

export function CardItem({ card, onOpen }: CardItemProps) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: card.id,
  });

  const style = transform
    ? {
        transform: `translate3d(${transform.x}px, ${transform.y}px, 0)`,
      }
    : undefined;

  return (
    <div
      ref={setNodeRef}
      style={style}
      {...listeners}
      {...attributes}
      onClick={() => onOpen(card.id)}
      className={`flex cursor-grab flex-col gap-2 rounded-lg border border-white/10 bg-gray-950/70 p-3 shadow-sm backdrop-blur-sm active:cursor-grabbing ${
        isDragging ? "z-10 opacity-60" : ""
      }`}
    >
      <p className="text-sm font-medium text-white">{card.title}</p>
      {card.description ? <p className="line-clamp-2 text-xs text-gray-300">{card.description}</p> : null}
      <div className="flex flex-wrap items-center gap-2">
        <span className="rounded-full bg-gray-800 px-2 py-0.5 text-[11px] text-gray-300">
          {TYPE_LABELS[card.cardType]}
        </span>
        <span className={`rounded-full px-2 py-0.5 text-[11px] ${PRIORITY_STYLES[card.priority]}`}>
          {card.priority}
        </span>
        {card.linkedBranch ? (
          <span className="rounded-full bg-emerald-900 px-2 py-0.5 text-[11px] text-emerald-300">
            {card.linkedBranch}
          </span>
        ) : null}
      </div>
    </div>
  );
}

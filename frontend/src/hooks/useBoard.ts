import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { appsApi, cardsApi } from "../services/api";
import type { Board, MoveCardRequest, UpdateCardRequest } from "../types";

export function boardQueryKey(appId: string) {
  return ["board", appId] as const;
}

export function useBoard(appId: string | undefined) {
  return useQuery({
    queryKey: boardQueryKey(appId ?? ""),
    queryFn: () => appsApi.getBoard(appId as string),
    enabled: Boolean(appId),
  });
}

interface MoveCardVariables {
  cardId: string;
  payload: MoveCardRequest;
}

export function useMoveCard(appId: string | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cardId, payload }: MoveCardVariables) => cardsApi.move(cardId, payload),
    onMutate: async ({ cardId, payload }: MoveCardVariables) => {
      if (!appId) return undefined;
      const key = boardQueryKey(appId);
      await queryClient.cancelQueries({ queryKey: key });
      const previous = queryClient.getQueryData<Board>(key);

      if (previous) {
        const next: Board = {
          ...previous,
          columns: previous.columns.map((column) => ({ ...column, cards: [...column.cards] })),
        };
        let movedCard = null;
        for (const column of next.columns) {
          const index = column.cards.findIndex((card) => card.id === cardId);
          if (index !== -1) {
            [movedCard] = column.cards.splice(index, 1);
            break;
          }
        }
        if (movedCard) {
          const target = next.columns.find((column) => column.id === payload.targetColumnId);
          if (target) {
            target.cards.push({
              ...movedCard,
              linkedBranch: payload.linkedBranch ?? movedCard.linkedBranch,
            });
          }
        }
        queryClient.setQueryData<Board>(key, next);
      }

      return { previous };
    },
    onError: (_error, _variables, context) => {
      if (!appId) return;
      const key = boardQueryKey(appId);
      const ctx = context as { previous?: Board } | undefined;
      if (ctx?.previous) {
        queryClient.setQueryData<Board>(key, ctx.previous);
      }
    },
    onSettled: () => {
      if (!appId) return;
      void queryClient.invalidateQueries({ queryKey: boardQueryKey(appId) });
    },
  });
}

interface UpdateCardVariables {
  cardId: string;
  payload: UpdateCardRequest;
}

export function useUpdateCard(appId: string | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cardId, payload }: UpdateCardVariables) => cardsApi.update(cardId, payload),
    onSuccess: () => {
      if (!appId) return;
      void queryClient.invalidateQueries({ queryKey: boardQueryKey(appId) });
    },
  });
}

export function useDeleteCard(appId: string | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (cardId: string) => cardsApi.remove(cardId),
    onSuccess: () => {
      if (!appId) return;
      void queryClient.invalidateQueries({ queryKey: boardQueryKey(appId) });
    },
  });
}

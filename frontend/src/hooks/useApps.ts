import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { appsApi } from "../services/api";
import type { CreateAppRequest } from "../types";

export const appsQueryKey = ["apps"] as const;

export function useApps() {
  return useQuery({
    queryKey: appsQueryKey,
    queryFn: () => appsApi.list(),
  });
}

export function useCreateApp() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreateAppRequest) => appsApi.create(payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: appsQueryKey });
    },
  });
}

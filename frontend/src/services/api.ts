import { apiClient } from "./apiClient";
import type {
  AppSummary,
  Board,
  Card,
  CreateAppRequest,
  CreateCardRequest,
  LoginRequest,
  LoginResponse,
  MoveCardRequest,
  UpdateCardRequest,
} from "../types";

export const authApi = {
  login: (payload: LoginRequest) => apiClient.post<LoginResponse>("/auth/login", payload),
};

export const appsApi = {
  list: () => apiClient.get<AppSummary[]>("/apps"),
  create: (payload: CreateAppRequest) => apiClient.post<AppSummary>("/apps", payload),
  getBoard: (appId: string) => apiClient.get<Board>(`/apps/${appId}/board`),
};

export const cardsApi = {
  create: (payload: CreateCardRequest) => apiClient.post<Card>("/cards", payload),
  move: (cardId: string, payload: MoveCardRequest) => apiClient.patch<Card>(`/cards/${cardId}/move`, payload),
  update: (cardId: string, payload: UpdateCardRequest) => apiClient.put<Card>(`/cards/${cardId}`, payload),
  remove: (cardId: string) => apiClient.delete<void>(`/cards/${cardId}`),
};

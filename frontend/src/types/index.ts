export type CardType = "feature" | "bug" | "chore" | "refactor";

export type Priority = "low" | "medium" | "high" | "urgent";

export interface AppSummary {
  id: string;
  name: string;
  color: string;
  repoUrl?: string | null;
}

export interface Card {
  id: string;
  title: string;
  description?: string | null;
  cardType: CardType;
  priority: Priority;
  linkedBranch?: string | null;
}

export interface BoardColumn {
  id: string;
  name: string;
  order: number;
  wipLimit: number | null;
  cards: Card[];
}

export interface Board {
  columns: BoardColumn[];
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
}

export interface CreateAppRequest {
  name: string;
  color: string;
  repoUrl?: string;
}

export interface CreateCardRequest {
  appId: string;
  columnId: string;
  title: string;
  description?: string;
  cardType: CardType;
  priority: Priority;
}

export interface MoveCardRequest {
  targetColumnId: string;
  linkedBranch?: string;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  [key: string]: unknown;
}

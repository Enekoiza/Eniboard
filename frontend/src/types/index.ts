export type CardType = "feature" | "bug" | "chore" | "idea";

export type Priority = "low" | "medium" | "high" | "critical";

export interface AppSummary {
  id: string;
  name: string;
  color: string;
  repoUrl?: string | null;
}

export interface Card {
  id: string;
  columnId: string;
  title: string;
  description?: string | null;
  cardType: CardType;
  priority: Priority;
  linkedBranch?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface BoardColumn {
  id: string;
  name: string;
  order: number;
  wipLimit: number | null;
  cards: Card[];
}

export interface Board {
  id: string;
  appId: string;
  columns: BoardColumn[];
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
}

export interface CreateAppRequest {
  name: string;
  color: string;
  repoUrl?: string;
}

export interface CreateCardRequest {
  boardId: string;
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

export interface UpdateCardRequest {
  title: string;
  description?: string;
  cardType: CardType;
  priority: Priority;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  [key: string]: unknown;
}

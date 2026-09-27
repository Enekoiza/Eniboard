import { useAuthStore } from "../stores/authStore";
import type { ProblemDetails } from "../types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "";

export class ApiError extends Error {
  status: number;
  problem: ProblemDetails | null;

  constructor(status: number, problem: ProblemDetails | null, message: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }
}

interface RequestOptions {
  method?: "GET" | "POST" | "PATCH" | "PUT" | "DELETE";
  body?: unknown;
  signal?: AbortSignal;
}

async function request<TResponse>(path: string, options: RequestOptions = {}): Promise<TResponse> {
  const { method = "GET", body, signal } = options;
  const token = useAuthStore.getState().token;

  const headers: Record<string, string> = {
    Accept: "application/json",
  };
  if (body !== undefined) {
    headers["Content-Type"] = "application/json";
  }
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
    signal,
  });

  if (!response.ok) {
    let problem: ProblemDetails | null = null;
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      problem = null;
    }

    if (response.status === 401 && token && path !== "/auth/login") {
      useAuthStore.getState().logout();
    }

    const message = problem?.detail ?? problem?.title ?? `Request failed with status ${response.status}`;
    throw new ApiError(response.status, problem, message);
  }

  if (response.status === 204) {
    return undefined as TResponse;
  }

  const text = await response.text();
  if (!text) {
    return undefined as TResponse;
  }
  return JSON.parse(text) as TResponse;
}

export const apiClient = {
  get: <TResponse>(path: string, signal?: AbortSignal) => request<TResponse>(path, { method: "GET", signal }),
  post: <TResponse>(path: string, body?: unknown, signal?: AbortSignal) =>
    request<TResponse>(path, { method: "POST", body, signal }),
  patch: <TResponse>(path: string, body?: unknown, signal?: AbortSignal) =>
    request<TResponse>(path, { method: "PATCH", body, signal }),
  put: <TResponse>(path: string, body?: unknown, signal?: AbortSignal) =>
    request<TResponse>(path, { method: "PUT", body, signal }),
  delete: <TResponse>(path: string, signal?: AbortSignal) => request<TResponse>(path, { method: "DELETE", signal }),
};

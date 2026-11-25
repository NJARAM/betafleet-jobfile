import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getToken, setAuth } from './session';

const BASE = import.meta.env.VITE_API_BASE ?? '';

export class ApiError extends Error {
  status: number;
  constructor(message: string, status: number) { super(message); this.status = status; }
}

/** Calls the API with the signed-in user's token. Rule violations come back as ProblemDetails and surface as ApiError. */
export async function api<T = unknown>(path: string, init?: { method?: string; body?: unknown }): Promise<T> {
  const token = getToken();
  const res = await fetch(`${BASE}/api${path}`, {
    method: init?.method ?? (init?.body !== undefined ? 'POST' : 'GET'),
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: init?.body !== undefined ? JSON.stringify(init.body) : undefined
  });
  if (!res.ok) {
    let msg = `${res.status} ${res.statusText}`;
    try { const p = await res.json(); msg = p.title ?? p.detail ?? msg; } catch { /* not JSON */ }
    // Expired, revoked or disabled session: drop it and show the sign-in screen.
    if (res.status === 401 && token && path !== '/auth/login') setAuth(null);
    throw new ApiError(res.status === 401 && token ? 'Your session has ended. Please sign in again.' : msg, res.status);
  }
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export function useApi<T>(path: string | null) {
  return useQuery<T>({ queryKey: [path], queryFn: () => api<T>(path!), enabled: path !== null });
}

/** A write that refreshes every query when it succeeds; screens stay consistent without hand-wiring keys. */
export function useAction<V = void, R = unknown>(fn: (v: V) => Promise<R>, onDone?: (r: R) => void) {
  const qc = useQueryClient();
  return useMutation<R, ApiError, V>({
    mutationFn: fn,
    onSuccess: async r => { await qc.invalidateQueries(); onDone?.(r); }
  });
}

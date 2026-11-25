import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';

export type User = { id: number; username: string; displayName: string; role: string; customerId?: number | null; customer?: string | null; mustChangePassword: boolean };
type Auth = { token: string; user: User };

export const ROLE_LABEL: Record<string, string> = {
  clerk: 'Ops clerk', 'ops-manager': 'Ops manager', 'unit-head': 'Unit head', finance: 'Finance', workshop: 'Workshop',
  storekeeper: 'Storekeeper', driver: 'Driver', customer: 'Customer', admin: 'Administrator'
};

const KEY = 'bf-auth';
let current: Auth | null = (() => {
  try { const a = JSON.parse(localStorage.getItem(KEY) ?? 'null'); if (a?.token && a?.user) return a; } catch { /* storage blocked */ }
  return null;
})();
const listeners = new Set<(a: Auth | null) => void>();

export const getToken = () => current?.token ?? null;

/** Single place that changes who is signed in; every subscriber (the app shell) re-renders. */
export function setAuth(a: Auth | null) {
  current = a;
  try { if (a) localStorage.setItem(KEY, JSON.stringify(a)); else localStorage.removeItem(KEY); } catch { /* ignore */ }
  listeners.forEach(l => l(a));
}

const Ctx = createContext<{ user: User | null; signOut: () => void }>({ user: null, signOut: () => {} });

export function SessionProvider({ children }: { children: ReactNode }) {
  const [auth, set] = useState(current);
  useEffect(() => { listeners.add(set); return () => { listeners.delete(set); }; }, []);
  return <Ctx.Provider value={{ user: auth?.user ?? null, signOut: () => setAuth(null) }}>{children}</Ctx.Provider>;
}

export const useSession = () => useContext(Ctx);

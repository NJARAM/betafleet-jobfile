import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { Link } from 'react-router';
import type { ApiError } from './api';

export const UNIT_NAMES: Record<string, string> = {
  C: 'Clearing & Forwarding', W: 'Warehousing', D: 'Distribution & Courier', T: 'Transit Transport', B: 'Billing', F: 'Job file'
};
export const UNIT_SHORT: Record<string, string> = { C: 'Clearing', W: 'Warehousing', D: 'Distribution', T: 'Transit', B: 'Billing', F: 'File' };
export const ORDER = ['C', 'W', 'D', 'T'];

export const money = (n: number, dp = 0) => '$' + Number(n ?? 0).toLocaleString('en-US', { minimumFractionDigits: dp, maximumFractionDigits: dp });
export const fmtDate = (iso?: string | null) => {
  if (!iso) return '—';
  const d = new Date(iso.length <= 10 ? iso + 'T00:00:00' : iso);
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short' });
};
export const fmtDateTime = (iso?: string | null) => iso ? new Date(iso).toLocaleString('en-GB', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }) : '—';

export function Unit({ u }: { u: string }) {
  if (u === 'B' || u === 'F') return <span className="svc" style={{ background: 'var(--gray-bg)', color: 'var(--gray)' }}>{u === 'B' ? '$' : 'F'}</span>;
  return <span className={`svc ${u}`} title={UNIT_NAMES[u]}>{u}</span>;
}

export function Chips({ units, all }: { units: string[]; all?: boolean }) {
  return <span className="chips">{ORDER.map(k => units.includes(k) ? <Unit key={k} u={k} /> : all ? <span key={k} className="svc off" title={`${UNIT_NAMES[k]} not used`}>{k}</span> : null)}</span>;
}

const PILL: Record<string, string> = {
  Opened: 'p-gray', Customs: 'p-info', Warehouse: 'p-info', Distribution: 'p-info', 'In transit': 'p-info', Billing: 'p-warn', Closed: 'p-ok',
  Draft: 'p-gray', 'Pending approval': 'p-warn', Approved: 'p-info', Rejected: 'p-bad', Disbursed: 'p-info', 'Converted to shipment order': 'p-ok',
  'Pending unit head': 'p-warn', 'Pending finance': 'p-warn', Paid: 'p-ok', Open: 'p-warn', Waiting: 'p-warn', Accepted: 'p-ok', Done: 'p-ok',
  Ready: 'p-ok', Accruing: 'p-gray', 'Awaiting POD': 'p-warn', Billed: 'p-info', Received: 'p-ok', Released: 'p-ok', Generated: 'p-ok', Signed: 'p-ok',
  Missing: 'p-bad', Pending: 'p-gray', Expired: 'p-bad', Expiring: 'p-warn', Valid: 'p-ok', 'n/a': 'p-gray', Posted: 'p-ok', Queued: 'p-warn', Failed: 'p-bad',
  'On route': 'p-info', Stopped: 'p-bad', Speeding: 'p-warn', Delivered: 'p-ok', Loading: 'p-gray', Available: 'p-ok', 'On trip': 'p-info', 'In workshop': 'p-warn',
  Assigned: 'p-info', Passed: 'p-ok', 'Job card opened': 'p-warn', 'In progress': 'p-info', 'Not started': 'p-gray', 'Awaiting parts': 'p-warn', Issued: 'p-ok',
  Fitted: 'p-info', Scrapped: 'p-bad', 'At retreader': 'p-warn', 'Awaiting retread': 'p-warn', Completed: 'p-ok', 'Within free time': 'p-ok',
  'Free time ending': 'p-warn', 'In detention': 'p-bad', Returned: 'p-ok', 'Returned late': 'p-warn'
};
export function Pill({ s, kind }: { s: string; kind?: string }) {
  const k = kind ?? PILL[s] ?? (s.startsWith('In store') ? 'p-ok' : 'p-gray');
  return <span className={`pill ${k}`}>{s}</span>;
}

export function JobLink({ id }: { id: string }) {
  return <Link className="idlink" to={`/jobs/${id}`}>{id}</Link>;
}

export function PageTop({ crumb, title, sub, right }: { crumb: string; title: string; sub?: string; right?: ReactNode }) {
  return <div className="top"><div><div className="crumb">{crumb}</div><h1>{title}</h1>{sub && <p className="sub">{sub}</p>}</div>{right}</div>;
}

export function Bar({ pct }: { pct: number }) {
  return <div className="row" style={{ flexWrap: 'nowrap' }}><div className="bar" style={{ flex: 1 }}><span style={{ width: `${pct}%` }} /></div><span className="small num muted">{pct}%</span></div>;
}

export function Kpi({ label, value, sub, tone = 'info' }: { label: string; value: ReactNode; sub?: string; tone?: 'info' | 'bad' | 'warn' | 'ok' | 'gray' }) {
  return (
    <div className="kpi">
      <span className="ic" style={{ background: `var(--${tone}-bg)`, color: `var(--${tone})` }}><svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="8" /></svg></span>
      <div><div className="l">{label}</div><div className="v num">{value}</div>{sub && <div className="s">{sub}</div>}</div>
    </div>
  );
}

export function Loading<T>({ q, children }: { q: { isLoading: boolean; error: unknown; data: T | undefined }; children: (d: T) => ReactNode }) {
  if (q.isLoading) return <div className="card muted">Loading…</div>;
  if (q.error) return <div className="err">{(q.error as Error).message}</div>;
  if (q.data === undefined) return null;
  return <>{children(q.data)}</>;
}

export function ErrorLine({ error }: { error: ApiError | null | undefined }) {
  return error ? <div className="err" role="alert">{error.message}</div> : null;
}

export function Modal({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
  useEffect(() => {
    const k = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', k); return () => window.removeEventListener('keydown', k);
  }, [onClose]);
  return (
    <div className="modal-bg" onMouseDown={e => { if (e.target === e.currentTarget) onClose(); }}>
      <div className="modal" role="dialog" aria-modal="true" aria-label={title}><h2>{title}</h2>{children}</div>
    </div>
  );
}

export function Tabs<T extends string>({ tabs, value, onChange }: { tabs: [T, ReactNode][]; value: T; onChange: (t: T) => void }) {
  return <div className="tabs" role="tablist">{tabs.map(([k, l]) => <button key={k} role="tab" aria-selected={value === k} className={value === k ? 'on' : ''} onClick={() => onChange(k)}>{l}</button>)}</div>;
}

export function Seg<T extends string>({ options, value, onChange, label }: { options: [T, string][]; value: T; onChange: (v: T) => void; label: string }) {
  return <div className="seg" role="group" aria-label={label}>{options.map(([k, l]) => <button key={k} className={value === k ? 'on' : ''} onClick={() => onChange(k)}>{l}</button>)}</div>;
}

// ---- toast ----
const ToastCtx = createContext<(msg: string) => void>(() => {});
export function ToastProvider({ children }: { children: ReactNode }) {
  const [msg, setMsg] = useState('');
  const t = useRef<number>(0);
  const show = (m: string) => { setMsg(m); clearTimeout(t.current); t.current = window.setTimeout(() => setMsg(''), 2800); };
  return <ToastCtx.Provider value={show}>{children}{msg && <div className="toast" role="status">{msg}</div>}</ToastCtx.Provider>;
}
export const useToast = () => useContext(ToastCtx);

import { useState, type ReactNode } from 'react';
import { Navigate, NavLink, Outlet, useLocation } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { useApi } from './api';
import { ROLE_LABEL, useSession } from './session';
import { Modal } from './ui';
import { ChangePasswordForm } from './pages/Login';

type Counts = { handoffsWaiting: number; fmPending: number; expensesPending: number; expiredDocs: number; openJobCards: number };

function Item({ to, children, count, bad, sub }: { to: string; children: ReactNode; count?: number; bad?: boolean; sub?: boolean }) {
  return (
    <NavLink className={({ isActive }) => `ni${sub ? ' nsub' : ''}${isActive ? ' on' : ''}`} to={to} end={to === '/'}>
      {children}
      {count ? <span className="cnt" style={bad ? { background: '#9b2a1d' } : undefined}>{count}</span> : null}
    </NavLink>
  );
}

const icon = (d: string) => <svg viewBox="0 0 24 24" aria-hidden="true"><path d={d} /></svg>;

/** Customers and drivers get one screen each; staff get the full workspace. The API enforces the same split. */
const ALLOWED: Record<string, string[]> = { customer: ['/portal'], driver: ['/driver'] };

export default function Layout() {
  const { user, signOut } = useSession();
  const qc = useQueryClient();
  const loc = useLocation();
  const staff = user!.role !== 'customer' && user!.role !== 'driver';
  const c = useApi<Counts>(staff ? '/dashboard' : null).data;
  const [open, setOpen] = useState<Record<string, boolean>>({ C: true, T: true });
  const [pw, setPw] = useState(false);
  const only = ALLOWED[user!.role];
  if (only && !only.includes(loc.pathname)) return <Navigate to={only[0]} replace />;

  const grp = (k: string, label: string, color: string) => (
    <button type="button" className="sgrp" aria-expanded={open[k]} onClick={() => setOpen(o => ({ ...o, [k]: !o[k] }))}>
      <span className="dot" style={{ background: color }} /><span style={{ flex: 1 }}>{label}</span>
      <svg className="chev" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" aria-hidden="true"><path d="M6 9l6 6 6-6" /></svg>
    </button>
  );

  return (
    <div className="app">
      <aside className="side" aria-label="Main navigation">
        <div className="brand">
          <span className="logo"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M3 7h11v9H3zM14 10h4l3 3v3h-7" /><circle cx="7" cy="18" r="2" /><circle cx="17" cy="18" r="2" /></svg></span>
          <div><div className="bn">BETAFLEET</div><div className="bs">INTEGRATED LOGISTICS</div></div>
        </div>
        {user!.role === 'customer' && <Item to="/portal">{icon('M3 12h18M12 3a14 14 0 010 18M12 3a14 14 0 000 18')}My shipments</Item>}
        {user!.role === 'driver' && <Item to="/driver">{icon('M7 2h10v20H7zM11 18h2')}My trip</Item>}
        {staff && <>
          <div className="grp">JOB FILES</div>
          <Item to="/">{icon('M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h7v7h-7z')}Control tower</Item>
          <Item to="/jobs">{icon('M3 6a2 2 0 012-2h4l2 2h8a2 2 0 012 2v10a2 2 0 01-2 2H5a2 2 0 01-2-2z')}Job files</Item>
          <Item to="/new-job">{icon('M12 5v14M5 12h14')}Open job file</Item>
          <Item to="/handoffs" count={c?.handoffsWaiting}>{icon('M7 7h11l-3-3M17 17H6l3 3')}Hand-over queue</Item>
          <div className="grp">BUSINESS UNITS</div>
          {grp('C', 'Clearing & Forwarding', 'var(--c)')}
          {open.C && <><Item sub to="/clearing">Clearing entries</Item><Item sub to="/containers">Container tracking</Item></>}
          {grp('T', 'Transit Transport', 'var(--t)')}
          {open.T && <>
            <Item sub to="/filling-masters" count={c?.fmPending}>Filling masters</Item>
            <Item sub to="/tracking">Shipments & tracking</Item>
            <Item sub to="/routes">Route master</Item>
          </>}
          <div className="grp">FINANCE</div>
          <Item to="/billing">{icon('M7 3h7l5 5v13H7zM14 3v5h5M10 13h6M10 17h6')}Billing</Item>
          <Item to="/expenses" count={c?.expensesPending}>{icon('M2 6h20v12H2zM12 9a3 3 0 100 6 3 3 0 000-6')}Expenses</Item>
          <Item to="/profitability">{icon('M4 20V10M10 20V4M16 20v-7M22 20H2')}Profitability</Item>
          <div className="grp">CUSTOMER</div>
          <Item to="/portal">{icon('M3 12h18M12 3a14 14 0 010 18M12 3a14 14 0 000 18')}Customer portal</Item>
          <div className="grp">FLEET</div>
          <Item to="/fleet">{icon('M14 6l4 4-8 8H6v-4zM12 8l4 4')}Fleet register</Item>
          <Item to="/workshop" count={c?.openJobCards}>{icon('M14.7 6.3a4 4 0 00-5.4 5.4L3 18l3 3 6.3-6.3a4 4 0 005.4-5.4l-2.5 2.5-2.5-.5-.5-2.5z')}Workshop</Item>
          <Item to="/tyres">{icon('M12 3a9 9 0 100 18 9 9 0 000-18M12 8.5a3.5 3.5 0 100 7 3.5 3.5 0 000-7')}Tyre management</Item>
          <Item to="/compliance" count={c?.expiredDocs} bad>{icon('M12 3l8 3v6c0 4.5-3.4 8.3-8 9-4.6-.7-8-4.5-8-9V6zM9 12l2 2 4-4')}Compliance</Item>
          <Item to="/driver">{icon('M7 2h10v20H7zM11 18h2')}Driver app</Item>
          {user!.role === 'admin' && <><div className="grp">ADMINISTRATION</div><Item to="/users">{icon('M16 19v-1a4 4 0 00-4-4H7a4 4 0 00-4 4v1M9.5 10a3.5 3.5 0 100-7 3.5 3.5 0 000 7M21 19v-1a4 4 0 00-3-3.9M16 3.1a4 4 0 010 7.8')}Users</Item></>}
        </>}
      </aside>
      <main className="main" id="view">
        <div className="topbar">
          <div className="who">
            <span><b>{user!.displayName}</b> <span className="muted">· {ROLE_LABEL[user!.role] ?? user!.role}{user!.customer ? ` · ${user!.customer}` : ''}</span></span>
            <button className="btn sm" onClick={() => setPw(true)}>Change password</button>
            <button className="btn sm" onClick={() => { qc.clear(); signOut(); }}>Sign out</button>
          </div>
        </div>
        <Outlet />
      </main>
      {pw && <Modal title="Change password" onClose={() => setPw(false)}><ChangePasswordForm onDone={() => setPw(false)} /></Modal>}
    </div>
  );
}

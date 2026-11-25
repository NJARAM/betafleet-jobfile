import { useState, type FormEvent, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ApiError, api } from '../api';
import { setAuth, useSession, type User } from '../session';

type AuthResult = { token: string; user: User };

function Shell({ title, sub, children }: { title: string; sub: string; children: ReactNode }) {
  return (
    <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center', background: 'var(--navy)', padding: 16 }}>
      <div className="card stack" style={{ width: '100%', maxWidth: 400, padding: 28 }}>
        <div className="brand" style={{ padding: 0 }}>
          <span className="logo" style={{ background: 'var(--navy)' }}><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M3 7h11v9H3zM14 10h4l3 3v3h-7" /><circle cx="7" cy="18" r="2" /><circle cx="17" cy="18" r="2" /></svg></span>
          <div><div className="bn" style={{ color: 'var(--navy)' }}>BETAFLEET</div><div className="bs" style={{ color: 'var(--muted)' }}>INTEGRATED LOGISTICS</div></div>
        </div>
        <div><h1 style={{ fontSize: 22 }}>{title}</h1><p className="sub">{sub}</p></div>
        {children}
      </div>
    </div>
  );
}

export function Login() {
  const qc = useQueryClient();
  const [username, setU] = useState('');
  const [password, setP] = useState('');
  const [err, setErr] = useState('');
  const [busy, setBusy] = useState(false);
  const submit = async (e: FormEvent) => {
    e.preventDefault(); setErr(''); setBusy(true);
    try {
      const r = await api<AuthResult>('/auth/login', { body: { username, password } });
      qc.clear();
      setAuth(r);
    } catch (x) {
      setErr(x instanceof ApiError && x.status === 429 ? 'Too many attempts from this device. Wait a minute and try again.' : (x as Error).message);
      setP('');
    } finally { setBusy(false); }
  };
  return (
    <Shell title="Sign in" sub="Job files, fleet and finance in one place.">
      <form className="stack" style={{ gap: 14 }} onSubmit={submit}>
        <label className="fl">Username<input className="inp" autoComplete="username" autoFocus value={username} onChange={e => setU(e.target.value)} /></label>
        <label className="fl">Password<input className="inp" type="password" autoComplete="current-password" value={password} onChange={e => setP(e.target.value)} /></label>
        {err && <div className="err" role="alert">{err}</div>}
        <button className="btn pri" disabled={busy || !username || !password}>{busy ? 'Signing in…' : 'Sign in'}</button>
      </form>
      <p className="muted small" style={{ margin: 0 }}>Forgot your password? Ask an administrator to reset it.</p>
    </Shell>
  );
}

export function ChangePasswordForm({ forced, onDone }: { forced?: boolean; onDone?: () => void }) {
  const { user, signOut } = useSession();
  const [cur, setCur] = useState('');
  const [next, setNext] = useState('');
  const [again, setAgain] = useState('');
  const [err, setErr] = useState('');
  const [busy, setBusy] = useState(false);
  const mismatch = again.length > 0 && next !== again;
  const submit = async (e: FormEvent) => {
    e.preventDefault(); setErr(''); setBusy(true);
    try { const r = await api<AuthResult>('/auth/change-password', { body: { currentPassword: cur, newPassword: next } }); setAuth(r); onDone?.(); }
    catch (x) { setErr((x as Error).message); }
    finally { setBusy(false); }
  };
  const form = (
    <form className="stack" style={{ gap: 14 }} onSubmit={submit}>
      <label className="fl">{forced ? 'Temporary password' : 'Current password'}<input className="inp" type="password" autoComplete="current-password" value={cur} onChange={e => setCur(e.target.value)} /></label>
      <label className="fl">New password<input className="inp" type="password" autoComplete="new-password" value={next} onChange={e => setNext(e.target.value)} /></label>
      <label className="fl">Repeat new password<input className="inp" type="password" autoComplete="new-password" value={again} onChange={e => setAgain(e.target.value)} /></label>
      <p className="muted small" style={{ margin: 0 }}>At least 10 characters, with letters and digits. Other sessions are signed out.</p>
      {mismatch && <div className="err">The two new passwords don't match.</div>}
      {err && <div className="err" role="alert">{err}</div>}
      <button className="btn pri" disabled={busy || !cur || !next || next !== again}>Change password</button>
      {forced && <button type="button" className="btn" onClick={signOut}>Sign out</button>}
    </form>
  );
  return forced ? <Shell title="Choose a new password" sub={`Welcome, ${user?.displayName}. Your account has a temporary password; set your own to continue.`}>{form}</Shell> : form;
}

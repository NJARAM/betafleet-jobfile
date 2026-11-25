import { useState } from 'react';
import { api, useAction, useApi } from '../api';
import { ROLE_LABEL, useSession } from '../session';
import { ErrorLine, Loading, Modal, PageTop, Pill, fmtDateTime, useToast } from '../ui';

type U = { id: number; username: string; displayName: string; role: string; customerId?: number; customer?: string; active: boolean; mustChangePassword: boolean; lastLoginAt?: string; locked: boolean };
type Cust = { id: number; name: string };
const ROLES = Object.keys(ROLE_LABEL);

export default function Users() {
  const q = useApi<U[]>('/users');
  const [edit, setEdit] = useState<U | 'new' | null>(null);
  return (
    <>
      <PageTop crumb="Administration" title="Users" sub="Accounts, roles and access. New accounts and password resets get a temporary password the user must change at first sign-in."
        right={<button className="btn pri" onClick={() => setEdit('new')}>+ New user</button>} />
      <section className="card tight">
        <Loading q={q}>{rows => <div className="scroll"><table style={{ minWidth: 820 }}>
          <thead><tr><th>User</th><th>Role</th><th>Last sign-in</th><th>Status</th><th /></tr></thead>
          <tbody>{rows.map(u => <tr key={u.id}>
            <td><b>{u.displayName}</b><div className="muted small mono">{u.username}</div></td>
            <td>{ROLE_LABEL[u.role] ?? u.role}{u.customer && <div className="muted small">{u.customer}</div>}</td>
            <td className="small">{u.lastLoginAt ? fmtDateTime(u.lastLoginAt) : 'Never'}</td>
            <td><div className="row" style={{ gap: 4 }}>
              <Pill s={u.active ? 'Active' : 'Disabled'} kind={u.active ? 'p-ok' : 'p-gray'} />
              {u.locked && <Pill s="Locked out" kind="p-bad" />}
              {u.mustChangePassword && <Pill s="Temporary password" kind="p-warn" />}
            </div></td>
            <td><button className="btn sm" onClick={() => setEdit(u)}>Edit</button></td>
          </tr>)}</tbody></table></div>}</Loading>
      </section>
      {edit && <UserModal u={edit === 'new' ? null : edit} onClose={() => setEdit(null)} />}
    </>
  );
}

function UserModal({ u, onClose }: { u: U | null; onClose: () => void }) {
  const { user: me } = useSession();
  const toast = useToast();
  const customers = useApi<Cust[]>('/customers').data ?? [];
  const [f, setF] = useState({ username: '', displayName: u?.displayName ?? '', role: u?.role ?? 'clerk', customerId: u?.customerId ?? null as number | null, active: u?.active ?? true, password: '' });
  const self = u?.id === me?.id;
  const save = useAction(() => u
    ? api(`/users/${u.id}`, { method: 'PUT', body: { displayName: f.displayName, role: f.role, customerId: f.customerId, active: f.active, newPassword: f.password || null } })
    : api('/users', { body: { username: f.username, displayName: f.displayName, role: f.role, customerId: f.customerId, password: f.password } }),
    () => { toast(u ? `${u.username} updated` : `${f.username} created`); onClose(); });
  return (
    <Modal title={u ? `Edit ${u.username}` : 'New user'} onClose={onClose}>
      {!u && <label className="fl">Username<input className="inp mono" autoComplete="off" value={f.username} onChange={e => setF({ ...f, username: e.target.value.toLowerCase() })} /></label>}
      <label className="fl">Display name (shown on audit trails)<input className="inp" value={f.displayName} onChange={e => setF({ ...f, displayName: e.target.value })} /></label>
      <label className="fl">Role<select className="inp" value={f.role} disabled={self} onChange={e => setF({ ...f, role: e.target.value })}>{ROLES.map(r => <option key={r} value={r}>{ROLE_LABEL[r]}</option>)}</select></label>
      {f.role === 'customer' && <label className="fl">Customer<select className="inp" value={f.customerId ?? ''} onChange={e => setF({ ...f, customerId: e.target.value ? +e.target.value : null })}>
        <option value="">Pick a customer…</option>{customers.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>}
      <label className="fl">{u ? 'Reset password (leave empty to keep)' : 'Temporary password'}<input className="inp" type="password" autoComplete="new-password" value={f.password} onChange={e => setF({ ...f, password: e.target.value })} /></label>
      {u && !self && <label className="row"><input type="checkbox" checked={f.active} onChange={e => setF({ ...f, active: e.target.checked })} /> Account active (unticking signs the user out everywhere)</label>}
      <p className="muted small" style={{ margin: 0 }}>Passwords: 10+ characters with letters and digits. The user must change a new or reset password at sign-in.</p>
      <ErrorLine error={save.error} />
      <div className="actions"><button className="btn" onClick={onClose}>Cancel</button><button className="btn pri" disabled={save.isPending} onClick={() => save.mutate()}>{u ? 'Save' : 'Create user'}</button></div>
    </Modal>
  );
}

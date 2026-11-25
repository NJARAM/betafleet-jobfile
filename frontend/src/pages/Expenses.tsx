import { useState } from 'react';
import { api, useAction, useApi } from '../api';
import { ErrorLine, JobLink, Loading, Modal, PageTop, Pill, Seg, Tabs, UNIT_SHORT, Unit, fmtDate, fmtDateTime, money, useToast } from '../ui';
import type { Activity, Expense, JobRow, Lookups } from '../types';

export default function Expenses() {
  const [tab, setTab] = useState<'list' | 'new'>('list');
  return (
    <>
      <PageTop crumb="Finance" title="Expense wizard" sub="Job expenses by unit. Unit head approves; above $1,000 Finance approves too; Finance pays. Rechargeable expenses become disbursement lines to bill." />
      <section className="card tight">
        <Tabs value={tab} onChange={setTab} tabs={[['list', 'Expenses'], ['new', '+ New expense']]} />
        <div className="tabbody">{tab === 'list' ? <List /> : <NewExpense onDone={() => setTab('list')} />}</div>
      </section>
    </>
  );
}

function List() {
  const [st, setSt] = useState('');
  const q = useApi<Expense[]>(`/expenses${st ? `?status=${encodeURIComponent(st)}` : ''}`);
  const [open, setOpen] = useState<string | null>(null);
  const toast = useToast();
  const act = useAction((v: { id: string; path: string; body?: unknown; msg: string }) => api(`/expenses/${v.id}/${v.path}`, { body: v.body ?? {} }).then(() => v.msg), m => toast(m));
  return <div className="stack">
    <Seg label="Status" value={st} onChange={setSt} options={[['', 'All'], ['Pending unit head', 'Unit head'], ['Pending finance', 'Finance'], ['Approved', 'To pay'], ['Paid', 'Paid'], ['Rejected', 'Rejected']]} />
    <ErrorLine error={act.error} />
    <Loading q={q}>{rows => <div className="scroll"><table style={{ minWidth: 980 }}>
      <thead><tr><th>Expense</th><th>Job file</th><th>Unit &amp; type</th><th>Payee</th><th className="r">Amount</th><th>Status</th><th /></tr></thead>
      <tbody>{rows.map(e => <tr key={e.id}>
        <td className="mono small"><button className="idlink" style={{ border: 0, cursor: 'pointer' }} onClick={() => setOpen(e.id)}>{e.id}</button><div className="muted">{fmtDate(e.date)} · {e.requestedBy}</div></td>
        <td><JobLink id={e.jobId} /></td><td><Unit u={e.unit} /> {e.type}</td>
        <td>{e.payee}<div className="muted small">{e.reference}{e.rechargeable ? ' · rechargeable' : ''}</div></td>
        <td className="r num">{e.currency} {e.amount.toLocaleString('en-US', { minimumFractionDigits: 2 })}</td>
        <td><Pill s={e.status} />{e.paymentVoucher && <div className="muted small">{e.paymentVoucher}</div>}</td>
        <td><div className="row" style={{ flexWrap: 'nowrap' }}>
          {(e.status === 'Pending unit head' || e.status === 'Pending finance') && <>
            <button className="btn sm pri" disabled={act.isPending} onClick={() => act.mutate({ id: e.id, path: 'approve', msg: `${e.id} approved` })}>Approve</button>
            <button className="btn sm bad" disabled={act.isPending} onClick={() => { const c = prompt('Reason for rejecting?'); if (c) act.mutate({ id: e.id, path: 'reject', body: { comment: c }, msg: `${e.id} rejected` }); }}>Reject</button></>}
          {e.status === 'Approved' && <button className="btn sm pri" disabled={act.isPending} onClick={() => act.mutate({ id: e.id, path: 'pay', body: {}, msg: `${e.id} paid` })}>Pay</button>}
        </div></td>
      </tr>)}
        {rows.length === 0 && <tr><td colSpan={7} className="empty">No expenses in this view.</td></tr>}
      </tbody></table></div>}</Loading>
    {open && <History id={open} onClose={() => setOpen(null)} />}
  </div>;
}

function History({ id, onClose }: { id: string; onClose: () => void }) {
  const q = useApi<{ expense: Expense; history: Activity[] }>(`/expenses/${id}`);
  return <Modal title={`Expense ${id}`} onClose={onClose}>
    <Loading q={q}>{d => <>
      <dl className="kv"><dt>Job</dt><dd>{d.expense.jobId}</dd><dt>Type</dt><dd>{d.expense.type}</dd><dt>Payee</dt><dd>{d.expense.payee}</dd><dt>Amount</dt><dd>{d.expense.currency} {d.expense.amount}</dd><dt>Notes</dt><dd>{d.expense.notes || '—'}</dd></dl>
      <div>{d.history.map(h => <div key={h.id} className="chk"><span className="small muted" style={{ minWidth: 110 }}>{fmtDateTime(h.at)}</span><div className="small"><b>{h.actor}</b> · {h.text}</div></div>)}
        {d.history.length === 0 && <p className="muted small">No audit entries.</p>}</div>
    </>}</Loading>
    <div className="actions"><button className="btn" onClick={onClose}>Close</button></div>
  </Modal>;
}

function NewExpense({ onDone }: { onDone: () => void }) {
  const jobs = (useApi<JobRow[]>('/jobs').data ?? []).filter(j => j.stage !== 'Closed');
  const lk = useApi<Lookups>('/lookups').data;
  const toast = useToast();
  const [f, setF] = useState({ jobId: '', unit: '', type: '', payee: '', amount: '', currency: 'USD', reference: '', rechargeable: false, notes: '' });
  const job = jobs.find(j => j.id === f.jobId) ?? jobs[0];
  const unit = job?.units.includes(f.unit) ? f.unit : job?.units[0] ?? 'C';
  const types = lk?.expenseTypes[unit] ?? [];
  const type = types.includes(f.type) ? f.type : types[0] ?? '';
  const amt = parseFloat(f.amount) || 0;
  const save = useAction(() => api<{ id: string }>('/expenses', { body: { ...f, jobId: job?.id, unit, type, amount: amt } }), r => { toast(`${r.id} submitted to unit head`); onDone(); });
  return <div className="stack" style={{ maxWidth: 720 }}>
    <div className="fgrid">
      <label className="fl">Job file<select className="inp" value={job?.id ?? ''} onChange={e => setF({ ...f, jobId: e.target.value })}>{jobs.map(j => <option key={j.id} value={j.id}>{j.id} · {j.customer}</option>)}</select></label>
      <label className="fl">Unit<select className="inp" value={unit} onChange={e => setF({ ...f, unit: e.target.value })}>{job?.units.map(u => <option key={u} value={u}>{UNIT_SHORT[u]}</option>)}</select></label>
      <label className="fl">Expense type<select className="inp" value={type} onChange={e => setF({ ...f, type: e.target.value })}>{types.map(t => <option key={t}>{t}</option>)}</select></label>
      <label className="fl">Payee<input className="inp" value={f.payee} onChange={e => setF({ ...f, payee: e.target.value })} /></label>
      <label className="fl">Amount<input className="inp num" inputMode="decimal" value={f.amount} onChange={e => setF({ ...f, amount: e.target.value })} /></label>
      <label className="fl">Currency<select className="inp" value={f.currency} onChange={e => setF({ ...f, currency: e.target.value })}><option>USD</option><option>TZS</option><option>ZMW</option></select></label>
      <label className="fl">Reference (receipt, invoice)<input className="inp" value={f.reference} onChange={e => setF({ ...f, reference: e.target.value })} /></label>
    </div>
    <label className="row"><input type="checkbox" checked={f.rechargeable} onChange={e => setF({ ...f, rechargeable: e.target.checked })} /> Recharge to customer (paid on behalf — billed at cost)</label>
    <label className="fl">Notes<textarea className="inp" value={f.notes} onChange={e => setF({ ...f, notes: e.target.value })} /></label>
    <div className="muted small">Approval route: unit head{amt > 1000 ? ` → Finance (over ${money(1000)})` : ''} → Finance pays.</div>
    <ErrorLine error={save.error} />
    <div><button className="btn pri" disabled={save.isPending || !job} onClick={() => save.mutate()}>Submit expense</button></div>
  </div>;
}

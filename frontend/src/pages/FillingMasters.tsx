import { useEffect, useState } from 'react';
import { api, useAction, useApi } from '../api';
import { useSession } from '../session';
import { ErrorLine, JobLink, Loading, Modal, PageTop, Pill, fmtDateTime, money, useToast } from '../ui';
import type { Activity, FitCheck, JobRow, Lookups } from '../types';

type Summary = { id: string; jobId: string; truckRequest: string; truck?: string; trailer?: string; driver?: string; routeCode: string; status: string; payRef?: string; shipmentOrderId?: string; standard: number; requested: number; variancePct: number };
type Line = { id: number; code: string; name: string; basis: string; qty: number; rate: number; unitLabel: string; standard: number; requested: number; reason: string; variancePct: number; over: boolean };
type Detail = { summary: Summary; customer: string; payMethod: string; comment?: string; flow: string[]; tolerance: number; lines: Line[]; fitChecks: FitCheck[]; history: Activity[] };

export default function FillingMasters() {
  const list = useApi<Summary[]>('/filling-masters');
  const [sel, setSel] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const current = sel ?? list.data?.[0]?.id ?? null;
  return (
    <>
      <PageTop crumb="Transit Transport" title="Filling masters" sub="Per-truck trip budget from the route master. Clerk prepares, Ops manager approves, Finance disburses, then it becomes a shipment order."
        right={<button className="btn pri" onClick={() => setCreating(true)}>+ New filling master</button>} />
      <div className="grid2 g-list">
        <section className="card tight">
          <Loading q={list}>{rows => <div className="listsel">{rows.map(f => <button key={f.id} className={f.id === current ? 'on' : ''} onClick={() => setSel(f.id)}>
            <span className="row" style={{ justifyContent: 'space-between' }}><b className="mono">{f.id}</b><Pill s={f.status} /></span>
            <span className="small muted">{f.jobId} · {f.truck ?? 'Truck to assign'} · {money(f.requested)}{Math.abs(f.variancePct) > 0 && ` (${f.variancePct > 0 ? '+' : ''}${f.variancePct}%)`}</span>
          </button>)}</div>}</Loading>
        </section>
        {current && <FmDetail id={current} />}
      </div>
      {creating && <CreateModal onClose={id => { setCreating(false); if (id) setSel(id); }} />}
    </>
  );
}

function FmDetail({ id }: { id: string }) {
  const q = useApi<Detail>(`/filling-masters/${id}`);
  const lk = useApi<Lookups>('/lookups').data;
  const session = useSession().user!;
  const toast = useToast();
  const [edit, setEdit] = useState<Record<number, { requested: number; reason: string }>>({});
  const [asg, setAsg] = useState({ truck: '', trailer: '', driver: '' });
  const [comment, setComment] = useState('');
  const [payRef, setPayRef] = useState('');
  useEffect(() => {
    if (!q.data) return;
    const s = q.data.summary;
    setAsg({ truck: s.truck ?? '', trailer: s.trailer ?? '', driver: s.driver ?? '' });
    setEdit(Object.fromEntries(q.data.lines.map(l => [l.id, { requested: l.requested, reason: l.reason }])));
  }, [q.data]);

  const act = useAction((v: { path: string; body?: unknown; msg: string }) => api(`/filling-masters/${id}${v.path}`, { method: v.path ? 'POST' : 'PUT', body: v.body ?? {} }).then(() => v.msg), m => toast(m));
  const save = () => act.mutateAsync({ path: '', msg: 'Saved', body: { ...asg, lines: Object.entries(edit).map(([lid, e]) => ({ id: +lid, requested: e.requested, reason: e.reason })) } });

  return (
    <Loading q={q}>{d => {
      const s = d.summary; const editable = (s.status === 'Draft' || s.status === 'Rejected') && (session.role === 'clerk' || session.role === 'admin');
      const total = d.lines.reduce((a, l) => a + (editable ? edit[l.id]?.requested ?? l.requested : l.requested), 0);
      const stepIdx = d.flow.indexOf(s.status);
      return <div className="stack">
        <section className="card stack">
          <div className="row" style={{ justifyContent: 'space-between' }}>
            <div><h2 className="mono">{s.id}</h2><div className="muted small"><JobLink id={s.jobId} /> · {d.customer} · {s.truckRequest} · route {s.routeCode}</div></div>
            <Pill s={s.status} />
          </div>
          <div className="stepper">{d.flow.map((f, i) => <div key={f} className="stp" style={{ background: i <= stepIdx ? 'var(--info-bg)' : undefined }}><span className="s">{i + 1}. {f}</span></div>)}</div>
          {d.comment && <div className={s.status === 'Rejected' ? 'err' : 'okmsg'}>Manager: {d.comment}</div>}
          <div className="fgrid">
            {(['truck', 'trailer', 'driver'] as const).map(k => <label key={k} className="fl" style={{ textTransform: 'capitalize' }}>{k}
              {editable && lk ? <select className="inp" value={asg[k]} onChange={e => setAsg({ ...asg, [k]: e.target.value })}>
                <option value="">To assign</option>
                {(k === 'truck' ? lk.vehicles.filter(v => v.type.startsWith('Horse')).map(v => v.reg) : k === 'trailer' ? lk.trailers : lk.drivers).map(o => <option key={o}>{o}</option>)}
              </select> : <span className="inp" style={{ display: 'flex', alignItems: 'center' }}>{asg[k] || '—'}</span>}
            </label>)}
          </div>
        </section>
        <section className="card tight">
          <div className="card-h"><h2>Trip expense lines</h2><span className="small muted">Lines more than {d.tolerance}% off standard need a reason</span></div>
          <div className="scroll"><table style={{ minWidth: 820 }}>
            <thead><tr><th>Line</th><th>Basis</th><th className="r">Standard</th><th className="r">Requested</th><th className="r">Var.</th><th>Reason</th></tr></thead>
            <tbody>{d.lines.map(l => {
              const e = edit[l.id] ?? { requested: l.requested, reason: l.reason };
              const v = l.standard ? (e.requested - l.standard) / l.standard * 100 : e.requested ? 100 : 0;
              const over = Math.abs(v) > d.tolerance;
              return <tr key={l.id}>
                <td><b>{l.name}</b><div className="muted small mono">{l.code}</div></td>
                <td className="small">{l.basis}<div className="muted">{l.qty} {l.unitLabel} × {money(l.rate, 2)}</div></td>
                <td className="r num">{money(l.standard, 2)}</td>
                <td className="r">{editable ? <input className="inp sm num" style={{ width: 110, textAlign: 'right' }} value={e.requested} inputMode="decimal"
                  onChange={x => setEdit({ ...edit, [l.id]: { ...e, requested: parseFloat(x.target.value) || 0 } })} /> : <span className="num">{money(l.requested, 2)}</span>}</td>
                <td className="r num" style={{ color: over ? 'var(--bad)' : undefined }}>{v === 0 ? '—' : `${v > 0 ? '+' : ''}${v.toFixed(1)}%`}</td>
                <td>{editable && over ? <input className="inp sm" placeholder="Why?" value={e.reason} onChange={x => setEdit({ ...edit, [l.id]: { ...e, reason: x.target.value } })} /> : <span className="small">{l.reason}</span>}</td>
              </tr>;
            })}
              <tr><td colSpan={2}><b>Total</b></td><td className="r num"><b>{money(s.standard, 2)}</b></td><td className="r num"><b>{money(total, 2)}</b></td><td colSpan={2} /></tr>
            </tbody></table></div>
        </section>
        <section className="card stack">
          <h2>Fitness checks</h2>
          {d.fitChecks.map(c => <div key={c.key} className="chk"><Pill s={c.ok ? 'OK' : 'Fail'} kind={c.ok ? 'p-ok' : 'p-bad'} /><div><b>{c.label}</b><div className="muted small">{c.detail}</div></div></div>)}
        </section>
        <section className="card stack">
          <h2>Actions</h2>
          <ErrorLine error={act.error} />
          {editable && <div className="actions" style={{ justifyContent: 'flex-start' }}>
            <button className="btn" disabled={act.isPending} onClick={() => save()}>Save draft</button>
            <button className="btn pri" disabled={act.isPending} onClick={async () => { try { await save(); } catch { return; } act.mutate({ path: '/submit', msg: 'Submitted for approval' }); }}>Save &amp; submit for approval</button>
          </div>}
          {s.status === 'Pending approval' && <>
            <label className="fl">Comment<input className="inp" value={comment} onChange={e => setComment(e.target.value)} placeholder="Required when rejecting" /></label>
            <div className="actions" style={{ justifyContent: 'flex-start' }}>
              <button className="btn pri" disabled={act.isPending} onClick={() => act.mutate({ path: '/approve', body: { comment }, msg: 'Approved · released to Finance' })}>Approve</button>
              <button className="btn bad" disabled={act.isPending} onClick={() => act.mutate({ path: '/reject', body: { comment }, msg: 'Rejected · back to clerk' })}>Reject</button>
            </div></>}
          {s.status === 'Approved' && <div className="row">
            <input className="inp" style={{ maxWidth: 280 }} placeholder="EFT / fuel card ref, e.g. EFT-77130 / FC-5540" value={payRef} onChange={e => setPayRef(e.target.value)} />
            <button className="btn pri" disabled={act.isPending} onClick={() => act.mutate({ path: '/disburse', body: { payRef }, msg: `Disbursed ${money(s.requested, 2)}` })}>Disburse funds</button>
          </div>}
          {s.status === 'Disbursed' && <div><button className="btn pri" disabled={act.isPending} onClick={() => act.mutate({ path: '/convert', msg: 'Shipment order created · truck dispatched' })}>Convert to shipment order</button></div>}
          {s.shipmentOrderId && <p className="small" style={{ margin: 0 }}>Shipment order <b className="mono">{s.shipmentOrderId}</b> · paid via {d.payMethod} · {s.payRef}</p>}
          <p className="muted small" style={{ margin: 0 }}>Signed in as {session.displayName} ({session.role}). The server enforces who can do each step.</p>
        </section>
        <section className="card tight">
          <div className="card-h"><h2>Audit trail</h2></div>
          {d.history.map(h => <div key={h.id} className="list-i"><span className="small muted" style={{ minWidth: 110 }}>{fmtDateTime(h.at)}</span><div><b className="small">{h.actor}</b><div className="small">{h.text}</div></div></div>)}
        </section>
      </div>;
    }}</Loading>
  );
}

function CreateModal({ onClose }: { onClose: (id?: string) => void }) {
  const jobs = (useApi<JobRow[]>('/jobs?unit=T').data ?? []).filter(j => j.stage !== 'Closed');
  const lk = useApi<Lookups>('/lookups').data;
  const [jobId, setJob] = useState('');
  const [route, setRoute] = useState('DSM-LUN-TUN');
  const create = useAction(() => api<{ id: string }>('/filling-masters', { body: { jobId: jobId || jobs[0]?.id, routeCode: route } }), r => onClose(r.id));
  return (
    <Modal title="New filling master" onClose={() => onClose()}>
      <label className="fl">Job file (transit leg)<select className="inp" value={jobId} onChange={e => setJob(e.target.value)}>{jobs.map(j => <option key={j.id} value={j.id}>{j.id} · {j.customer}</option>)}</select></label>
      <label className="fl">Route<select className="inp" value={route} onChange={e => setRoute(e.target.value)}>{lk?.routes.map(r => <option key={r.code} value={r.code}>{r.code} · {r.name}</option>)}</select></label>
      <p className="muted small" style={{ margin: 0 }}>Expense lines are loaded from the route's current standard rates.</p>
      <ErrorLine error={create.error} />
      <div className="actions"><button className="btn" onClick={() => onClose()}>Cancel</button><button className="btn pri" disabled={create.isPending || !jobs.length} onClick={() => create.mutate()}>Create</button></div>
    </Modal>
  );
}

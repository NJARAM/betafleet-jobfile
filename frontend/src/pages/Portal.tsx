import { useState } from 'react';
import { useNavigate } from 'react-router';
import { api, useAction, useApi } from '../api';
import { useSession } from '../session';
import { Bar, Chips, ErrorLine, Loading, ORDER, PageTop, Pill, Tabs, UNIT_NAMES, fmtDate, money, useToast } from '../ui';
import type { ContainerRow, Lookups } from '../types';

type Overview = {
  customer: { id: number; name: string; creditLimit: number; openBalance: number };
  jobs: { id: string; reference: string; cargo: string; stage: string; progressPct: number; eta: string; units: string[];
    milestones: { date: string; unit: string; title: string; done: boolean }[]; documents: { name: string; reference: string; status: string }[];
    trips: { id: string; truck: string; place: string; progressPct: number; eta: string; status: string }[]; containers: ContainerRow[] }[];
  invoices: { no: string; date: string; sbu: string; status: string; jobs: string[]; total: number }[];
  requests: { id: string; services: string; cargo: string; from: string; to: string; date: string; step: number; jobId?: string }[];
  steps: string[];
};
type OpsReq = { id: string; customer: string; services: string; cargo: string; weight: string; from: string; to: string; date: string; notes: string; step: number; jobId?: string };

export default function Portal() {
  const me = useSession().user!;
  const isCustomer = me.role === 'customer';
  const lk = useApi<Lookups>(isCustomer ? null : '/lookups');
  const [tab, setTab] = useState<'customer' | 'ops'>('customer');
  // A customer account sees only its own company, with no staff tabs.
  if (isCustomer) return (
    <>
      <PageTop crumb={me.customer ?? 'Customer'} title="My shipments" sub="Your files, trucks on the road, documents and invoices, and a form to request transport." />
      <section className="card"><CustomerView fixedId={me.customerId!} /></section>
    </>
  );
  return (
    <>
      <PageTop crumb="Customer" title="Customer portal" sub="What a customer sees: their files, live trips, documents and invoices, and a form to request transport. Ops reviews requests on the second tab." />
      <section className="card tight">
        <Tabs value={tab} onChange={setTab} tabs={[['customer', 'Customer view'], ['ops', 'Requests (ops review)']]} />
        <div className="tabbody"><Loading q={lk}>{l => tab === 'customer' ? <CustomerView l={l} /> : <OpsView />}</Loading></div>
      </section>
    </>
  );
}

function CustomerView({ l, fixedId }: { l?: Lookups; fixedId?: number }) {
  const [picked, setCid] = useState(l?.customers[0]?.id ?? 0);
  const cid = fixedId ?? picked;
  const q = useApi<Overview>(`/portal/${cid}`);
  const [open, setOpen] = useState<string | null>(null);
  return <div className="stack">
    {l && <label className="fl" style={{ maxWidth: 360 }}>Preview as customer<select className="inp" value={cid} onChange={e => setCid(+e.target.value)}>{l.customers.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>}
    <Loading q={q}>{d => <>
      <div className="kpis">
        <div className="kpi"><div><div className="l">Active shipments</div><div className="v">{d.jobs.filter(j => j.stage !== 'Closed').length}</div></div></div>
        <div className="kpi"><div><div className="l">Open invoices</div><div className="v">{money(d.invoices.filter(i => i.status === 'Open').reduce((a, i) => a + i.total, 0), 2)}</div></div></div>
        <div className="kpi"><div><div className="l">Credit available</div><div className="v">{money(Math.max(0, d.customer.creditLimit - d.customer.openBalance))}</div></div></div>
      </div>
      <h3>Your shipments</h3>
      {d.jobs.map(j => <div key={j.id} className="card stack" style={{ padding: 16 }}>
        <button className="row" style={{ all: 'unset', cursor: 'pointer', display: 'flex', gap: 12, flexWrap: 'wrap', justifyContent: 'space-between' }} onClick={() => setOpen(open === j.id ? null : j.id)}>
          <span><b className="mono">{j.id}</b> · {j.cargo}<div className="muted small">{j.reference} · ETA {j.eta}</div></span>
          <span className="row"><Chips units={j.units} /><Pill s={j.stage} /><span style={{ width: 140 }}><Bar pct={j.progressPct} /></span></span>
        </button>
        {open === j.id && <div className="grid2">
          <div className="tl">{j.milestones.map((m, i) => <div key={i} className="ev"><div className="when">{fmtDate(m.date)}</div><div className={`rail c${m.unit}`}><span className={`dotc${m.done ? '' : ' todo'}`} />{i < j.milestones.length - 1 && <span className="line" />}</div><div className="what"><b>{m.title}</b></div></div>)}</div>
          <div className="stack">
            {j.trips.length > 0 && <div><div className="eyebrow">Trucks</div>{j.trips.map(t => <div key={t.id} className="chk"><Pill s={t.status} /><div className="small"><b>{t.truck}</b> · {t.place} · ETA {t.eta}</div></div>)}</div>}
            {j.containers.length > 0 && <div><div className="eyebrow">Containers</div>{j.containers.map(c => <div key={c.id} className="chk"><span className="mono small">{c.number}</span><span className="small">{c.status}</span></div>)}</div>}
            <div><div className="eyebrow">Documents</div>{j.documents.map((x, i) => <div key={i} className="chk small"><Pill s={x.status} />{x.name} <span className="muted mono">{x.reference}</span></div>)}{j.documents.length === 0 && <p className="muted small">None shared yet.</p>}</div>
          </div>
        </div>}
      </div>)}
      <h3>Invoices</h3>
      <table><thead><tr><th>Invoice</th><th>Date</th><th>Job files</th><th className="r">Total</th><th>Status</th></tr></thead><tbody>
        {d.invoices.map(i => <tr key={i.no}><td className="mono">INV {i.no}</td><td className="small">{fmtDate(i.date)}</td><td className="mono small">{i.jobs.join(', ')}</td><td className="r num">{money(i.total, 2)}</td><td><Pill s={i.status} /></td></tr>)}
      </tbody></table>
      <h3>Transport requests</h3>
      {d.requests.map(r => <div key={r.id} className="stack" style={{ gap: 6 }}>
        <div className="small"><b className="mono">{r.id}</b> · {r.cargo} · {r.from} → {r.to} · {fmtDate(r.date)}</div>
        <div className="stepper">{d.steps.map((s, i) => <div key={s} className="stp" style={{ background: i <= r.step ? 'var(--ok-bg)' : undefined }}><span className="s">{s}</span></div>)}</div>
      </div>)}
      <RequestForm customerId={cid} />
    </>}</Loading>
  </div>;
}

function RequestForm({ customerId }: { customerId: number }) {
  const toast = useToast();
  const [f, setF] = useState({ units: { C: true, T: true } as Record<string, boolean>, cargo: '', weight: '', from: 'Dar es Salaam port', to: '', date: new Date(Date.now() + 5 * 864e5).toISOString().slice(0, 10), reference: '', notes: '' });
  const send = useAction(() => api<{ id: string }>('/portal/requests', { body: { ...f, customerId, units: ORDER.filter(u => f.units[u]) } }), r => { toast(`${r.id} submitted · BetaFleet ops will review it`); setF({ ...f, cargo: '', to: '', notes: '' }); });
  return <section className="card stack" style={{ padding: 16 }}>
    <h3>Request transport</h3>
    <div className="row">{ORDER.map(u => <label key={u} className="row small"><input type="checkbox" checked={!!f.units[u]} onChange={e => setF({ ...f, units: { ...f.units, [u]: e.target.checked } })} />{UNIT_NAMES[u]}</label>)}</div>
    <div className="fgrid">
      <label className="fl">Cargo<input className="inp" value={f.cargo} onChange={e => setF({ ...f, cargo: e.target.value })} /></label>
      <label className="fl">Weight<input className="inp" value={f.weight} onChange={e => setF({ ...f, weight: e.target.value })} /></label>
      <label className="fl">From<input className="inp" value={f.from} onChange={e => setF({ ...f, from: e.target.value })} /></label>
      <label className="fl">To<input className="inp" value={f.to} onChange={e => setF({ ...f, to: e.target.value })} /></label>
      <label className="fl">Ready date<input className="inp" type="date" value={f.date} onChange={e => setF({ ...f, date: e.target.value })} /></label>
      <label className="fl">BL / container ref<input className="inp mono" value={f.reference} onChange={e => setF({ ...f, reference: e.target.value })} /></label>
    </div>
    <label className="fl">Notes<input className="inp" value={f.notes} onChange={e => setF({ ...f, notes: e.target.value })} /></label>
    <ErrorLine error={send.error} />
    <div><button className="btn pri" disabled={send.isPending} onClick={() => send.mutate()}>Submit request</button></div>
  </section>;
}

function OpsView() {
  const q = useApi<OpsReq[]>('/portal/requests');
  const nav = useNavigate();
  const toast = useToast();
  const review = useAction((id: string) => api(`/portal/requests/${id}/review`, { method: 'POST' }));
  const open = useAction((id: string) => api<{ jobId: string }>(`/portal/requests/${id}/open-job`, { method: 'POST' }), r => { toast(`${r.jobId} opened from portal request`); nav(`/jobs/${r.jobId}`); });
  return <Loading q={q}>{rows => <>
    <ErrorLine error={review.error ?? open.error} />
    <div className="scroll"><table style={{ minWidth: 860 }}>
      <thead><tr><th>Request</th><th>Customer</th><th>Cargo</th><th>Lane</th><th>Ready</th><th>Step</th><th /></tr></thead>
      <tbody>{rows.map(r => <tr key={r.id}>
        <td className="mono small"><b>{r.id}</b><div className="muted">{r.services.split('').join(' · ')}</div></td><td>{r.customer}</td>
        <td>{r.cargo}<div className="muted small">{r.weight} {r.notes}</div></td><td className="small">{r.from} → {r.to}</td><td className="small">{fmtDate(r.date)}</td>
        <td><Pill s={['Submitted', 'Under review', 'Approved', 'Job file opened', 'In transit', 'Delivered'][r.step]} kind={r.step >= 3 ? 'p-ok' : 'p-warn'} />{r.jobId && <div className="mono small">{r.jobId}</div>}</td>
        <td>{r.step < 2 && <button className="btn sm" disabled={review.isPending} onClick={() => review.mutate(r.id)}>{r.step === 0 ? 'Start review' : 'Approve'}</button>}
          {r.step === 2 && <button className="btn sm pri" disabled={open.isPending} onClick={() => open.mutate(r.id)}>Open job file</button>}</td>
      </tr>)}</tbody></table></div>
  </>}</Loading>;
}

import { useState } from 'react';
import { api, useAction, useApi } from '../api';
import { ErrorLine, Loading, PageTop, Pill, Tabs, fmtDate, money, useToast } from '../ui';
import type { Lookups } from '../types';

const CHECKLIST: [string, string[]][] = [
  ['Engine & fluids', ['Engine oil level', 'Coolant level', 'Fan belt condition']],
  ['Brakes & air system', ['Air pressure build-up', 'Air leaks / hoses', 'Brake chambers & slack adjusters', 'Service brake test']],
  ['Electrical & lights', ['Headlights', 'Indicators & hazards', 'Brake lights']],
  ['Steering & suspension', ['Steering play', 'Shock absorbers']],
  ['Tyres & wheels', ['Wheel nuts', 'Tyre condition & pressure']],
  ['Cab & safety', ['Windscreen & wipers', 'Mirrors', 'Fire extinguisher', 'Seat belts']]
];
type Ins = { id: string; vehicle: string; type: string; date: string; odometerKm: number; by: string; status: string; jobCardId?: string; defects: { item: string; note: string }[] };
type Task = { id: number; name: string; category: string; mechanic: string; status: string; hours: number; partCode?: string };
type Card = { id: string; vehicle: string; source: string; openedOn: string; odometerKm: number; closed: boolean; tasks: Task[];
  requests: { id: string; taskId: number; partCode: string; qty: number; status: string; requestedBy: string; issueNo?: string }[]; labourCost: number; partsCost: number };
type Part = { code: string; name: string; bin: string; stock: number; cost: number; reorder: number };

export default function Workshop() {
  const [tab, setTab] = useState<'cards' | 'ins' | 'stores'>('cards');
  return (
    <>
      <PageTop crumb="Fleet" title="Workshop" sub="Inspection → job card → tasks and spare parts → close. A truck with an open job card cannot be dispatched." />
      <section className="card tight">
        <Tabs value={tab} onChange={setTab} tabs={[['cards', 'Job cards'], ['ins', 'Inspections'], ['stores', 'Stores']]} />
        <div className="tabbody">{tab === 'cards' ? <Cards /> : tab === 'ins' ? <Inspections /> : <Stores />}</div>
      </section>
    </>
  );
}

function Cards() {
  const q = useApi<Card[]>('/workshop/job-cards');
  const parts = useApi<Part[]>('/workshop/parts').data ?? [];
  const mechs = useApi<string[]>('/workshop/mechanics').data ?? [];
  const toast = useToast();
  const [sel, setSel] = useState<string | null>(null);
  const [req, setReq] = useState<{ taskId: number; part: string; qty: number } | null>(null);
  const act = useAction((v: { path: string; method?: string; body?: unknown; msg?: string }) => api(`/workshop${v.path}`, { method: v.method ?? 'POST', body: v.body ?? {} }).then(() => v.msg), m => m && toast(m));
  return <Loading q={q}>{rows => {
    const c = rows.find(r => r.id === sel) ?? rows[0];
    return <div className="grid2 g-list">
      <div className="listsel card tight">{rows.map(r => <button key={r.id} className={r.id === c?.id ? 'on' : ''} onClick={() => setSel(r.id)}>
        <span className="row" style={{ justifyContent: 'space-between' }}><b className="mono">{r.id}</b><Pill s={r.closed ? 'Closed' : 'Open'} kind={r.closed ? 'p-ok' : 'p-warn'} /></span>
        <span className="small muted">{r.vehicle} · {r.tasks.filter(t => t.status === 'Done').length}/{r.tasks.length} tasks · {fmtDate(r.openedOn)}</span></button>)}</div>
      {c && <div className="stack">
        <div className="row" style={{ justifyContent: 'space-between' }}>
          <div><h2>{c.id} · {c.vehicle}</h2><div className="muted small">From {c.source} · {c.odometerKm.toLocaleString()} km · labour {money(c.labourCost, 2)} · parts {money(c.partsCost, 2)}</div></div>
          {!c.closed && <button className="btn pri" disabled={act.isPending} onClick={() => act.mutate({ path: `/job-cards/${c.id}/close`, msg: `${c.id} closed · ${c.vehicle} released` })}>Close job card</button>}
        </div>
        <ErrorLine error={act.error} />
        <table><thead><tr><th>Task</th><th>Mechanic</th><th>Status</th><th className="r">Hours</th><th>Parts</th></tr></thead><tbody>
          {c.tasks.map(t => <tr key={t.id}>
            <td><b>{t.name}</b><div className="muted small">{t.category}</div></td>
            <td>{c.closed ? t.mechanic : <select className="inp sm" value={t.mechanic} onChange={e => act.mutate({ path: `/job-cards/${c.id}/tasks/${t.id}`, method: 'PUT', body: { mechanic: e.target.value } })}>{[t.mechanic, ...mechs.filter(m => m !== t.mechanic)].map(m => <option key={m}>{m}</option>)}</select>}</td>
            <td>{c.closed ? <Pill s={t.status} /> : <select className="inp sm" value={t.status} onChange={e => act.mutate({ path: `/job-cards/${c.id}/tasks/${t.id}`, method: 'PUT', body: { status: e.target.value } })}>{['Not started', 'In progress', 'Awaiting parts', 'Done'].map(s => <option key={s}>{s}</option>)}</select>}</td>
            <td className="r">{c.closed ? t.hours : <input className="inp sm num" style={{ width: 70, textAlign: 'right' }} defaultValue={t.hours} onBlur={e => { const h = parseFloat(e.target.value) || 0; if (h !== t.hours) act.mutate({ path: `/job-cards/${c.id}/tasks/${t.id}`, method: 'PUT', body: { hours: h } }); }} />}</td>
            <td className="small">{c.requests.filter(r => r.taskId === t.id).map(r => <div key={r.id}>{r.qty} × {r.partCode} <Pill s={r.status} /></div>)}
              {!c.closed && <button className="btn sm" onClick={() => setReq({ taskId: t.id, part: t.partCode ?? parts[0]?.code ?? '', qty: 1 })}>Request part</button>}</td>
          </tr>)}
        </tbody></table>
        {req && <div className="card row" style={{ padding: 14 }}>
          <select className="inp" style={{ maxWidth: 320 }} value={req.part} onChange={e => setReq({ ...req, part: e.target.value })}>{parts.map(p => <option key={p.code} value={p.code}>{p.code} · {p.name} ({p.stock} in stock)</option>)}</select>
          <input className="inp num" style={{ width: 80 }} value={req.qty} onChange={e => setReq({ ...req, qty: parseInt(e.target.value) || 1 })} />
          <button className="btn pri" onClick={() => { act.mutate({ path: `/job-cards/${c.id}/part-requests`, body: { taskId: req.taskId, partCode: req.part, qty: req.qty }, msg: 'Part requested from stores' }); setReq(null); }}>Send to stores</button>
          <button className="btn" onClick={() => setReq(null)}>Cancel</button>
        </div>}
      </div>}
    </div>;
  }}</Loading>;
}

function Inspections() {
  const q = useApi<Ins[]>('/workshop/inspections');
  const lk = useApi<Lookups>('/lookups').data;
  const toast = useToast();
  const [veh, setVeh] = useState('');
  const [type, setType] = useState('Pre-trip');
  const [odo, setOdo] = useState('');
  const [bad, setBad] = useState<Record<string, string>>({});
  const vehicles = lk?.vehicles.filter(v => !v.subcontracted) ?? [];
  const v = veh || vehicles[0]?.reg;
  const save = useAction(() => api<Ins>('/workshop/inspections', { body: { vehicle: v, type, odometerKm: parseInt(odo.replace(/\D/g, '')) || 0, defects: Object.entries(bad).map(([item, note]) => ({ item, note, category: CHECKLIST.find(([, l]) => l.includes(item))![0] })) } }),
    r => { setBad({}); setOdo(''); toast(r.status === 'Passed' ? `${r.id} passed` : `${r.id} · job card ${r.jobCardId} opened`); });
  return <div className="grid2">
    <Loading q={q}>{rows => <table><thead><tr><th>Inspection</th><th>Vehicle</th><th>Date · by</th><th>Defects</th><th>Result</th></tr></thead><tbody>
      {rows.map(i => <tr key={i.id}><td className="mono small">{i.id}<div className="muted">{i.type}</div></td><td>{i.vehicle}</td><td className="small">{fmtDate(i.date)} · {i.by}</td>
        <td className="small">{i.defects.map(d => <div key={d.item}>{d.item}{d.note && <span className="muted"> — {d.note}</span>}</div>)}{i.defects.length === 0 && '—'}</td>
        <td><Pill s={i.status} />{i.jobCardId && <div className="mono small">{i.jobCardId}</div>}</td></tr>)}
    </tbody></table>}</Loading>
    <section className="card stack">
      <h3>New inspection</h3>
      <div className="fgrid">
        <label className="fl">Vehicle<select className="inp" value={v} onChange={e => setVeh(e.target.value)}>{vehicles.map(x => <option key={x.reg}>{x.reg}</option>)}</select></label>
        <label className="fl">Type<select className="inp" value={type} onChange={e => setType(e.target.value)}><option>Pre-trip</option><option>Post-trip</option></select></label>
        <label className="fl">Odometer (km)<input className="inp num" value={odo} onChange={e => setOdo(e.target.value)} /></label>
      </div>
      <div className="muted small">Tick anything that fails. Each failed item becomes a job-card task.</div>
      {CHECKLIST.map(([cat, items]) => <div key={cat}><div className="eyebrow">{cat}</div>
        {items.map(it => <div key={it} className="chk">
          <label className="row small" style={{ minWidth: 220 }}><input type="checkbox" checked={it in bad} onChange={e => { const n = { ...bad }; if (e.target.checked) n[it] = ''; else delete n[it]; setBad(n); }} />{it}</label>
          {it in bad && <input className="inp sm" placeholder="What is wrong?" value={bad[it]} onChange={e => setBad({ ...bad, [it]: e.target.value })} />}
        </div>)}</div>)}
      <ErrorLine error={save.error} />
      <button className="btn pri" disabled={save.isPending || !odo} onClick={() => save.mutate()}>{Object.keys(bad).length ? `Submit · open job card (${Object.keys(bad).length} defects)` : 'Submit · passed'}</button>
    </section>
  </div>;
}

function Stores() {
  const parts = useApi<Part[]>('/workshop/parts');
  const cards = useApi<Card[]>('/workshop/job-cards');
  const toast = useToast();
  const issue = useAction((id: string) => api(`/workshop/part-requests/${id}/issue`, { method: 'POST' }).then(() => id), id => toast(`${id} issued`));
  const pending = (cards.data ?? []).flatMap(c => c.requests.filter(r => r.status === 'Pending').map(r => ({ ...r, card: c.id, vehicle: c.vehicle })));
  return <div className="stack">
    <h3>Requests waiting at the store</h3>
    <ErrorLine error={issue.error} />
    <table><thead><tr><th>Request</th><th>Job card</th><th>Part</th><th className="r">Qty</th><th>By</th><th /></tr></thead><tbody>
      {pending.map(r => <tr key={r.id}><td className="mono small">{r.id}</td><td className="small">{r.card} · {r.vehicle}</td><td className="mono small">{r.partCode}</td><td className="r num">{r.qty}</td><td className="small">{r.requestedBy}</td>
        <td><button className="btn sm pri" disabled={issue.isPending} onClick={() => issue.mutate(r.id)}>Issue</button></td></tr>)}
      {pending.length === 0 && <tr><td colSpan={6} className="empty">No pending requests.</td></tr>}
    </tbody></table>
    <h3>Parts stock</h3>
    <Loading q={parts}>{rows => <table><thead><tr><th>Code</th><th>Part</th><th>Bin</th><th className="r">Stock</th><th className="r">Reorder at</th><th className="r">Unit cost</th></tr></thead><tbody>
      {rows.map(p => <tr key={p.code}><td className="mono small">{p.code}</td><td>{p.name}</td><td className="mono small">{p.bin}</td>
        <td className="r num" style={{ color: p.stock <= p.reorder ? 'var(--bad)' : undefined, fontWeight: p.stock <= p.reorder ? 700 : undefined }}>{p.stock}</td><td className="r num">{p.reorder}</td><td className="r num">{money(p.cost, 2)}</td></tr>)}
    </tbody></table>}</Loading>
  </div>;
}

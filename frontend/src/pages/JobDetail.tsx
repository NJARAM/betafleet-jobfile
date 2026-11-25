import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { api, useAction, useApi } from '../api';
import { Chips, ErrorLine, Loading, Modal, PageTop, Pill, Tabs, UNIT_NAMES, UNIT_SHORT, Unit, fmtDate, money, useToast } from '../ui';
import type { Charge, ContainerRow, Expense, Handoff, JobRow, Leg, Shipment, TimelineEvent } from '../types';

type Detail = {
  job: JobRow; legs: Leg[]; timeline: TimelineEvent[]; documents: { id: number; unit: string; name: string; reference: string; status: string }[];
  charges: Charge[]; expenses: Expense[]; clearing: { id: string; entryNo: string; entryType: string; stage: number; containers: string }[];
  containers: ContainerRow[]; shipments: Shipment[]; handoffs: Handoff[];
  profit: { revenue: number; cost: number; margin: number; marginPct: number };
};
type Tab = 'overview' | 'docs' | 'charges' | 'cargo' | 'handoffs';

export default function JobDetail() {
  const { id } = useParams();
  const q = useApi<Detail>(`/jobs/${id}`);
  const [tab, setTab] = useState<Tab>('overview');
  const [editLeg, setEditLeg] = useState<Leg | null>(null);
  const toast = useToast();
  const addUnit = useAction((u: string) => api(`/jobs/${id}/units/${u}`, { method: 'POST' }), () => toast('Unit added · hand-over created'));

  return (
    <Loading q={q}>{d => {
      const j = d.job;
      const ready = d.charges.filter(c => c.status === 'Ready' && !c.invoiceNo).reduce((s, c) => s + (c.kind === 'Disbursement' ? c.cost : c.revenue), 0);
      return <>
        <PageTop crumb={`Job files / ${j.id}`} title={j.id} sub={`${j.customer} · ${j.cargo}`} right={<Link className="btn" to="/jobs">All files</Link>} />
        <section className="card">
          <div className="jh">
            <div className="row"><Pill s={j.stage} /><Chips units={j.units} all />{j.alert && <span className="pill p-bad">{j.alert}</span>}</div>
          </div>
          <div className="jmeta">
            <div><b>Reference</b><span className="mono">{j.reference}</span></div>
            <div><b>Movement</b><span>{j.direction} · {j.cargoType}</span></div>
            <div><b>Opened</b><span>{fmtDate(j.openedOn)} · {j.owner}</span></div>
            <div><b>ETA</b><span>{j.eta || '—'}</span></div>
            <div><b>Progress</b><span>{j.progressPct}%</span></div>
            <div><b>Margin to date</b><span>{money(d.profit.margin)} ({d.profit.marginPct}%)</span></div>
          </div>
        </section>
        <div className="legs">
          {d.legs.map(l => l.off
            ? <div key={l.unit} className="leg off"><div className="lt"><span className="svc off">{l.unit}</span>{UNIT_SHORT[l.unit]}</div>
                <span className="muted small">Not on this file</span>
                {j.stage !== 'Closed' && <button className="btn sm" disabled={addUnit.isPending} onClick={() => addUnit.mutate(l.unit)}>+ Add {UNIT_SHORT[l.unit]}</button>}</div>
            : <button key={l.unit} className="leg" onClick={() => setEditLeg(l)} title="Update leg">
                <div className="lt"><Unit u={l.unit} />{UNIT_SHORT[l.unit]}</div>
                <span className="ln">{l.number}</span>
                <span className="row"><Pill s={l.status ?? ''} kind={l.progressPct === 100 ? 'p-ok' : 'p-info'} /><span className="small muted">{l.owner}</span></span>
                <div className="lb"><span style={{ width: `${l.progressPct}%`, background: `var(--${l.unit.toLowerCase()})` }} /></div>
                <span className="small">{l.summary}</span>
              </button>)}
        </div>
        <ErrorLine error={addUnit.error} />
        <section className="card tight">
          <Tabs value={tab} onChange={setTab} tabs={[['overview', 'Timeline'], ['docs', `Documents (${d.documents.length})`], ['charges', 'Charges & profit'], ['cargo', 'Cargo & trips'], ['handoffs', `Hand-overs (${d.handoffs.length})`]]} />
          <div className="tabbody">
            {tab === 'overview' && <div className="tl">
              {d.timeline.map((e, i) => <div key={e.id} className="ev">
                <div className="when">{fmtDate(e.date)}</div>
                <div className={`rail c${e.unit}`}><span className={`dotc${e.done ? '' : ' todo'}`} />{i < d.timeline.length - 1 && <span className="line" />}</div>
                <div className="what"><b>{e.title}</b><span className="muted small">{e.detail}</span></div>
              </div>)}
            </div>}
            {tab === 'docs' && <table><thead><tr><th>Unit</th><th>Document</th><th>Reference</th><th>Status</th></tr></thead><tbody>
              {d.documents.map(x => <tr key={x.id}><td><Unit u={x.unit} /></td><td>{x.name}</td><td className="mono small">{x.reference}</td><td><Pill s={x.status} /></td></tr>)}
              {d.documents.length === 0 && <tr><td colSpan={4} className="empty">No documents yet.</td></tr>}
            </tbody></table>}
            {tab === 'charges' && <div className="stack">
              <div className="kpis">
                <div className="kpi"><div><div className="l">Revenue</div><div className="v num">{money(d.profit.revenue)}</div></div></div>
                <div className="kpi"><div><div className="l">Cost</div><div className="v num">{money(d.profit.cost)}</div><div className="s">charges + approved job expenses</div></div></div>
                <div className="kpi"><div><div className="l">Margin</div><div className="v num">{money(d.profit.margin)}</div><div className="s">{d.profit.marginPct}%</div></div></div>
                <div className="kpi"><div><div className="l">Ready to invoice</div><div className="v num">{money(ready)}</div><div className="s"><Link to="/billing">Billing</Link></div></div></div>
              </div>
              <div className="scroll"><table style={{ minWidth: 680 }}><thead><tr><th>Unit</th><th>Line</th><th>Type</th><th className="r">Revenue</th><th className="r">Cost</th><th>Status</th></tr></thead><tbody>
                {d.charges.map(c => <tr key={c.id}><td><Unit u={c.unit} /></td><td>{c.description}</td><td className="small">{c.kind === 'Disbursement' ? 'Paid on behalf' : 'Service'}</td>
                  <td className="r num">{money(c.kind === 'Disbursement' ? c.cost : c.revenue)}</td><td className="r num">{money(c.cost)}</td>
                  <td><Pill s={c.status} />{c.invoiceNo && <span className="mono small muted"> INV {c.invoiceNo}</span>}</td></tr>)}
              </tbody></table></div>
              <h3>Job expenses</h3>
              <table><thead><tr><th>Expense</th><th>Unit</th><th>Type</th><th className="r">Amount</th><th>Status</th></tr></thead><tbody>
                {d.expenses.map(e => <tr key={e.id}><td className="mono small">{e.id}</td><td><Unit u={e.unit} /></td><td>{e.type}<div className="muted small">{e.payee}</div></td><td className="r num">{e.currency} {e.amount.toLocaleString()}</td><td><Pill s={e.status} /></td></tr>)}
                {d.expenses.length === 0 && <tr><td colSpan={5} className="empty">No expenses on this file.</td></tr>}
              </tbody></table>
            </div>}
            {tab === 'cargo' && <div className="stack">
              {d.clearing.length > 0 && <table><thead><tr><th>Clearing entry</th><th>Type</th><th>Entry no.</th><th>Containers</th><th>Stage</th></tr></thead><tbody>
                {d.clearing.map(c => <tr key={c.id}><td className="mono small">{c.id}</td><td>{c.entryType}</td><td className="mono small">{c.entryNo}</td><td>{c.containers}</td><td>{c.stage + 1} of 6</td></tr>)}</tbody></table>}
              {d.containers.length > 0 && <table><thead><tr><th>Container</th><th>Size</th><th>Status</th><th>Days out</th><th>Detention</th></tr></thead><tbody>
                {d.containers.map(c => <tr key={c.id}><td className="mono">{c.number}</td><td>{c.size}</td><td>{c.status}</td><td className="num">{c.daysOut}</td><td><Pill s={c.detentionState} /></td></tr>)}</tbody></table>}
              {d.shipments.length > 0 && <table><thead><tr><th>Shipment order</th><th>Truck</th><th>Driver</th><th>Position</th><th>Status</th></tr></thead><tbody>
                {d.shipments.map(s => <tr key={s.id}><td className="mono small">{s.id}</td><td>{s.truck}</td><td>{s.driver}</td><td>{s.place} · {s.progressPct}%</td><td><Pill s={s.status} /></td></tr>)}</tbody></table>}
              {d.clearing.length + d.containers.length + d.shipments.length === 0 && <p className="muted">No containers or trips on this file.</p>}
            </div>}
            {tab === 'handoffs' && <table><thead><tr><th>To</th><th>From</th><th>Hand-over</th><th>State</th></tr></thead><tbody>
              {d.handoffs.map(h => <tr key={h.id}><td><Unit u={h.unit} /></td><td><Unit u={h.fromUnit} /></td><td><b>{h.title}</b><div className="muted small">{h.note}</div></td><td><Pill s={h.state} />{h.acceptedBy && <div className="muted small">{h.acceptedBy}</div>}</td></tr>)}
            </tbody></table>}
          </div>
        </section>
        {editLeg && <LegModal jobId={j.id} leg={editLeg} onClose={() => setEditLeg(null)} />}
      </>;
    }}</Loading>
  );
}

function LegModal({ jobId, leg, onClose }: { jobId: string; leg: Leg; onClose: () => void }) {
  const [status, setStatus] = useState(leg.status ?? '');
  const [pct, setPct] = useState(leg.progressPct ?? 0);
  const [summary, setSummary] = useState(leg.summary ?? '');
  const save = useAction(() => api(`/jobs/${jobId}/legs/${leg.unit}`, { method: 'PUT', body: { status, progressPct: pct, summary } }), onClose);
  return (
    <Modal title={`${UNIT_NAMES[leg.unit]} · ${leg.number}`} onClose={onClose}>
      <label className="fl">Status<input className="inp" value={status} onChange={e => setStatus(e.target.value)} /></label>
      <label className="fl">Progress ({pct}%)<input type="range" min={0} max={100} step={5} value={pct} onChange={e => setPct(+e.target.value)} /></label>
      <label className="fl">Summary<input className="inp" value={summary} onChange={e => setSummary(e.target.value)} /></label>
      <ErrorLine error={save.error} />
      <div className="actions"><button className="btn" onClick={onClose}>Cancel</button><button className="btn pri" disabled={save.isPending} onClick={() => save.mutate()}>Save leg</button></div>
    </Modal>
  );
}

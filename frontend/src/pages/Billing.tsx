import { useState } from 'react';
import { api, useAction, useApi } from '../api';
import { ErrorLine, JobLink, Loading, Modal, PageTop, Pill, Seg, UNIT_SHORT, Unit, fmtDate, money, useToast } from '../ui';

type Line = { id: number; jobId: string; unit: string; description: string; kind: string; amount: number; status: string; customerId: number; customer: string };
type Billable = { lines: Line[]; customers: { id: number; name: string; creditLimit: number; openBalance: number }[]; vatRate: number };
type Inv = { no: string; date: string; customer: string; sbu: string; status: string; sapStatus: string; sapDocNo?: string; jobs: string[]; services: number; disbursements: number; vat: number; total: number };

export default function Billing() {
  const b = useApi<Billable>('/billing/billable');
  const inv = useApi<Inv[]>('/billing/invoices');
  const toast = useToast();
  const [cust, setCust] = useState<number | null>(null);
  const [picked, setPicked] = useState<Record<number, boolean>>({});
  const [mode, setMode] = useState<'consolidated' | 'per-sbu'>('consolidated');
  const [acc, setAcc] = useState(false);
  const raise = useAction((v: { customerId: number; ids: number[] }) => api<{ invoices: Inv[]; warning?: string }>('/billing/invoices', { body: { customerId: v.customerId, chargeIds: v.ids, mode } }),
    r => { setPicked({}); toast(`Raised ${r.invoices.map(i => 'INV ' + i.no).join(', ')} · queued for SAP${r.warning ? ' · ' + r.warning : ''}`); });
  const paid = useAction((no: string) => api(`/billing/invoices/${no}/paid`, { method: 'POST' }));

  return (
    <>
      <PageTop crumb="Finance" title="Billing" sub="Invoice what is ready, across units, as one consolidated invoice or one per business unit. Disbursements pass through at cost; VAT applies to services."
        right={<button className="btn" onClick={() => setAcc(true)}>+ Accessorial charge</button>} />
      <Loading q={b}>{d => {
        const withLines = d.customers.filter(c => d.lines.some(l => l.customerId === c.id));
        const cid = cust ?? withLines[0]?.id;
        const c = d.customers.find(x => x.id === cid);
        const lines = d.lines.filter(l => l.customerId === cid);
        const ready = lines.filter(l => l.status === 'Ready');
        const sel = ready.filter(l => picked[l.id] ?? true);
        const svc = sel.filter(l => l.kind !== 'Disbursement').reduce((a, l) => a + l.amount, 0);
        const disb = sel.filter(l => l.kind === 'Disbursement').reduce((a, l) => a + l.amount, 0);
        const vat = Math.round(svc * d.vatRate * 100) / 100;
        const total = svc + disb + vat;
        const over = c && c.openBalance + total > c.creditLimit;
        return <div className="grid2">
          <section className="card tight">
            <div className="card-h">
              <select className="inp" style={{ maxWidth: 320 }} aria-label="Customer" value={cid} onChange={e => { setCust(+e.target.value); setPicked({}); }}>
                {withLines.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}
              </select>
              <Seg label="Invoice mode" value={mode} onChange={setMode} options={[['consolidated', 'Consolidated'], ['per-sbu', 'One per unit']]} />
            </div>
            <div className="scroll"><table style={{ minWidth: 700 }}>
              <thead><tr><th /><th>Job file</th><th>Unit</th><th>Line</th><th className="r">Amount</th><th>Status</th></tr></thead>
              <tbody>{lines.map(l => <tr key={l.id}>
                <td>{l.status === 'Ready' && <input type="checkbox" aria-label={`Bill ${l.description}`} checked={picked[l.id] ?? true} onChange={e => setPicked({ ...picked, [l.id]: e.target.checked })} />}</td>
                <td><JobLink id={l.jobId} /></td><td><Unit u={l.unit} /></td>
                <td>{l.description}{l.kind === 'Disbursement' && <div className="muted small">Disbursement · no VAT</div>}</td>
                <td className="r num">{money(l.amount, 2)}</td><td><Pill s={l.status} /></td>
              </tr>)}
                {lines.length === 0 && <tr><td colSpan={6} className="empty">Nothing unbilled for this customer.</td></tr>}
              </tbody></table></div>
          </section>
          <aside className="card stack">
            <div className="eyebrow">Invoice preview · {mode === 'consolidated' ? 'one invoice' : `${new Set(sel.map(l => l.unit)).size} invoice(s) by unit`}</div>
            {mode === 'per-sbu' && [...new Set(sel.map(l => l.unit))].map(u => <div key={u} className="row small"><Unit u={u} />{UNIT_SHORT[u]} · {money(sel.filter(l => l.unit === u).reduce((a, l) => a + l.amount, 0), 2)}</div>)}
            <dl className="kv">
              <dt>Services</dt><dd className="num">{money(svc, 2)}</dd>
              <dt>VAT {Math.round(d.vatRate * 100)}%</dt><dd className="num">{money(vat, 2)}</dd>
              <dt>Disbursements</dt><dd className="num">{money(disb, 2)}</dd>
              <dt>Total</dt><dd className="num" style={{ fontSize: 18 }}>{money(total, 2)}</dd>
            </dl>
            {c && <div className={over ? 'err' : 'muted small'}>Credit: {money(c.openBalance)} open of {money(c.creditLimit)} limit{over ? ' — this invoice takes the customer over their limit' : ''}</div>}
            <ErrorLine error={raise.error} />
            <button className="btn pri" disabled={!sel.length || raise.isPending || !cid} onClick={() => raise.mutate({ customerId: cid!, ids: sel.map(l => l.id) })}>Raise invoice &amp; send to SAP</button>
            <p className="muted small" style={{ margin: 0 }}>Invoices go to SAP Business One through the integration bridge, with job file and unit on each line.</p>
          </aside>
        </div>;
      }}</Loading>
      <section className="card tight">
        <div className="card-h"><h2>Invoices</h2></div>
        <ErrorLine error={paid.error} />
        <Loading q={inv}>{rows => <div className="scroll"><table style={{ minWidth: 860 }}>
          <thead><tr><th>Invoice</th><th>Date</th><th>Customer</th><th>Type</th><th>Job files</th><th className="r">Total incl. VAT</th><th>SAP</th><th>Status</th><th /></tr></thead>
          <tbody>{rows.map(i => <tr key={i.no}>
            <td className="mono"><b>INV {i.no}</b></td><td className="small">{fmtDate(i.date)}</td><td>{i.customer}</td>
            <td>{i.sbu === 'ALL' ? <Pill s="Consolidated" kind="p-info" /> : <><Unit u={i.sbu} /> {UNIT_SHORT[i.sbu]}</>}</td>
            <td>{i.jobs.map(j => <div key={j}><JobLink id={j} /></div>)}</td><td className="r num">{money(i.total, 2)}</td>
            <td><Pill s={i.sapStatus} />{i.sapDocNo && <div className="muted small">{i.sapDocNo}</div>}</td><td><Pill s={i.status} /></td>
            <td>{i.status === 'Open' && <button className="btn sm" disabled={paid.isPending} onClick={() => paid.mutate(i.no)}>Mark paid</button>}</td>
          </tr>)}</tbody></table></div>}</Loading>
      </section>
      {acc && <AccessorialModal onClose={() => setAcc(false)} />}
    </>
  );
}

function AccessorialModal({ onClose }: { onClose: () => void }) {
  const jobs = useApi<{ id: string; customer: string; units: string[]; stage: string }[]>('/jobs').data?.filter(j => j.stage !== 'Closed') ?? [];
  const [f, setF] = useState({ jobId: '', unit: 'T', description: '', amount: '', reason: '' });
  const job = jobs.find(j => j.id === f.jobId) ?? jobs[0];
  const save = useAction(() => api('/billing/accessorial', { body: { ...f, jobId: job?.id, amount: parseFloat(f.amount) || 0 } }), onClose);
  return (
    <Modal title="Accessorial charge" onClose={onClose}>
      <label className="fl">Job file<select className="inp" value={job?.id ?? ''} onChange={e => setF({ ...f, jobId: e.target.value })}>{jobs.map(j => <option key={j.id} value={j.id}>{j.id} · {j.customer}</option>)}</select></label>
      <label className="fl">Unit<select className="inp" value={f.unit} onChange={e => setF({ ...f, unit: e.target.value })}>{(job?.units ?? []).map(u => <option key={u} value={u}>{UNIT_SHORT[u]}</option>)}</select></label>
      <label className="fl">Description<input className="inp" placeholder="Waiting time at Tunduma — 1 day" value={f.description} onChange={e => setF({ ...f, description: e.target.value })} /></label>
      <label className="fl">Amount (USD)<input className="inp num" inputMode="decimal" value={f.amount} onChange={e => setF({ ...f, amount: e.target.value })} /></label>
      <label className="fl">Reason (shown on the audit trail)<input className="inp" value={f.reason} onChange={e => setF({ ...f, reason: e.target.value })} /></label>
      <ErrorLine error={save.error} />
      <div className="actions"><button className="btn" onClick={onClose}>Cancel</button><button className="btn pri" disabled={save.isPending} onClick={() => save.mutate()}>Add charge</button></div>
    </Modal>
  );
}

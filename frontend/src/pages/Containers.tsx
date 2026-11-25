import { useState } from 'react';
import { api, useAction, useApi } from '../api';
import { ErrorLine, JobLink, Loading, Modal, PageTop, Pill, Seg, fmtDate, useToast } from '../ui';
import type { ContainerRow, Lookups } from '../types';

const NEXT = ['Gated out of port', 'Released to ICD', 'Delivered to warehouse · destuffed', 'Awaiting truck', 'Loaded on truck', 'Destuffed · awaiting empty return', 'Empty returned'];

export default function Containers() {
  const q = useApi<ContainerRow[]>('/containers');
  const lk = useApi<Lookups>('/lookups').data;
  const [f, setF] = useState('out');
  const [sel, setSel] = useState<ContainerRow | null>(null);
  const [add, setAdd] = useState(false);
  return (
    <>
      <PageTop crumb="Clearing & Forwarding" title="Container tracking" sub={`Every box from discharge to empty return. Shipping-line free time is 14 days; after that detention accrues.`}
        right={<button className="btn pri" onClick={() => setAdd(true)}>+ Add container</button>} />
      <Loading q={q}>{rows => {
        const list = rows.filter(r => f === 'all' || (f === 'out' ? !r.returnedOn : r.detentionDays > 0 || r.detentionState === 'Free time ending'));
        const det = rows.filter(r => r.detentionState === 'In detention').length;
        return <section className="card tight">
          <div className="card-h">
            <Seg label="Filter" value={f} onChange={setF} options={[['out', 'Not yet returned'], ['risk', 'Detention risk'], ['all', 'All']]} />
            <span className="muted small">{rows.filter(r => !r.returnedOn).length} out · <b style={{ color: 'var(--bad)' }}>{det} in detention</b></span>
          </div>
          <div className="scroll"><table style={{ minWidth: 900 }}>
            <thead><tr><th>Container</th><th>Job file</th><th>Status</th><th>Discharged</th><th className="r">Days out</th><th>Free time</th><th /></tr></thead>
            <tbody>{list.map(c => <tr key={c.id}>
              <td className="mono"><b>{c.number}</b><div className="muted small">{c.size} · {c.clearingId}</div></td>
              <td><JobLink id={c.jobId} /></td><td>{c.status}{c.depot && <div className="muted small">{c.depot}</div>}</td>
              <td className="small">{fmtDate(c.dischargedOn)}</td><td className="r num">{c.daysOut}</td>
              <td><Pill s={c.detentionState} />{c.detentionDays > 0 && <div className="small" style={{ color: 'var(--bad)' }}>{c.detentionDays} day(s) detention</div>}</td>
              <td>{!c.returnedOn && <button className="btn sm" onClick={() => setSel(c)}>Update</button>}</td>
            </tr>)}
              {list.length === 0 && <tr><td colSpan={7} className="empty">No containers in this view.</td></tr>}
            </tbody></table></div>
        </section>;
      }}</Loading>
      {sel && lk && <UpdateModal c={sel} depots={lk.depots} onClose={() => setSel(null)} />}
      {add && lk && <AddModal lk={lk} onClose={() => setAdd(false)} />}
    </>
  );
}

function UpdateModal({ c, depots, onClose }: { c: ContainerRow; depots: string[]; onClose: () => void }) {
  const [status, setStatus] = useState(NEXT[0]);
  const [depot, setDepot] = useState(depots[0]);
  const [note, setNote] = useState('');
  const toast = useToast();
  const save = useAction(() => api(`/containers/${c.id}`, { method: 'PUT', body: { status, depot: status === 'Empty returned' ? depot : null, note } }), () => { toast(`${c.number} · ${status}`); onClose(); });
  return (
    <Modal title={`Update ${c.number}`} onClose={onClose}>
      <div className="tl small">{c.events.map((e, i) => <div key={i}><b>{fmtDate(e.date)}</b> · {e.text}</div>)}</div>
      <label className="fl">New status<select className="inp" value={status} onChange={e => setStatus(e.target.value)}>{NEXT.map(s => <option key={s}>{s}</option>)}</select></label>
      {status === 'Empty returned' && <label className="fl">Empty depot<select className="inp" value={depot} onChange={e => setDepot(e.target.value)}>{depots.map(s => <option key={s}>{s}</option>)}</select></label>}
      <label className="fl">Note (EIR, truck, ICD …)<input className="inp" value={note} onChange={e => setNote(e.target.value)} /></label>
      <ErrorLine error={save.error} />
      <div className="actions"><button className="btn" onClick={onClose}>Cancel</button><button className="btn pri" disabled={save.isPending} onClick={() => save.mutate()}>Save</button></div>
    </Modal>
  );
}

function AddModal({ lk, onClose }: { lk: Lookups; onClose: () => void }) {
  const clr = useApi<{ id: string; jobId: string; customer: string }[]>('/clearing').data ?? [];
  const [f, setF] = useState({ clearingId: '', number: '', size: lk.containerSizes[1], dischargedOn: new Date().toISOString().slice(0, 10) });
  const entry = clr.find(c => c.id === f.clearingId) ?? clr[0];
  const save = useAction(() => api('/containers', { body: { ...f, clearingId: entry?.id, jobId: entry?.jobId } }), onClose);
  return (
    <Modal title="Add container" onClose={onClose}>
      <label className="fl">Clearing entry<select className="inp" value={entry?.id ?? ''} onChange={e => setF({ ...f, clearingId: e.target.value })}>{clr.map(c => <option key={c.id} value={c.id}>{c.id} · {c.jobId} · {c.customer}</option>)}</select></label>
      <label className="fl">Container number (ISO 6346)<input className="inp mono" placeholder="MSKU7741310" value={f.number} onChange={e => setF({ ...f, number: e.target.value.toUpperCase() })} /></label>
      <div className="fgrid">
        <label className="fl">Size<select className="inp" value={f.size} onChange={e => setF({ ...f, size: e.target.value })}>{lk.containerSizes.map(s => <option key={s}>{s}</option>)}</select></label>
        <label className="fl">Discharged<input className="inp" type="date" value={f.dischargedOn} onChange={e => setF({ ...f, dischargedOn: e.target.value })} /></label>
      </div>
      <ErrorLine error={save.error} />
      <div className="actions"><button className="btn" onClick={onClose}>Cancel</button><button className="btn pri" disabled={!f.number || save.isPending} onClick={() => save.mutate()}>Add</button></div>
    </Modal>
  );
}

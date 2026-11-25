import { useState } from 'react';
import { api, useAction, useApi } from '../api';
import { ErrorLine, Loading, Modal, PageTop, Pill, Seg, fmtDateTime, useToast } from '../ui';
import type { Activity } from '../types';

type Doc = { id: number; subjectType: string; subjectId: string; docType: string; expiresOn?: string; reference: string; daysLeft?: number; state: string };

export default function Compliance() {
  const q = useApi<Doc[]>('/compliance');
  const hist = useApi<Activity[]>('/compliance/history');
  const [type, setType] = useState('Vehicle');
  const [f, setF] = useState('issues');
  const [renew, setRenew] = useState<Doc | null>(null);
  return (
    <>
      <PageTop crumb="Fleet" title="Compliance" sub="Licences, insurance and permits for every truck and driver. An expired document blocks the truck or driver from new trips." />
      <Loading q={q}>{rows => {
        const list = rows.filter(r => r.subjectType === type && (f === 'all' || r.state === 'Expired' || r.state === 'Expiring'));
        return <section className="card tight">
          <div className="card-h">
            <Seg label="Subject" value={type} onChange={setType} options={[['Vehicle', 'Vehicles'], ['Driver', 'Drivers']]} />
            <Seg label="Filter" value={f} onChange={setF} options={[['issues', 'Expired & expiring'], ['all', 'All documents']]} />
            <span className="small"><b style={{ color: 'var(--bad)' }}>{rows.filter(r => r.state === 'Expired').length} expired</b> · {rows.filter(r => r.state === 'Expiring').length} expiring in 30 days</span>
          </div>
          <div className="scroll"><table style={{ minWidth: 760 }}>
            <thead><tr><th>{type}</th><th>Document</th><th>Reference</th><th>Expires</th><th className="r">Days left</th><th>State</th><th /></tr></thead>
            <tbody>{list.map(r => <tr key={r.id}>
              <td><b>{r.subjectId}</b></td><td>{r.docType}</td><td className="mono small">{r.reference}</td>
              <td className="small">{r.expiresOn ? new Date(r.expiresOn).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : 'n/a'}</td>
              <td className="r num" style={{ color: (r.daysLeft ?? 99) < 0 ? 'var(--bad)' : undefined }}>{r.daysLeft ?? '—'}</td><td><Pill s={r.state} /></td>
              <td>{r.state !== 'n/a' && <button className="btn sm" onClick={() => setRenew(r)}>Renew</button>}</td>
            </tr>)}
              {list.length === 0 && <tr><td colSpan={7} className="empty">Nothing expired or expiring.</td></tr>}
            </tbody></table></div>
        </section>;
      }}</Loading>
      <section className="card tight"><div className="card-h"><h2>Renewal history</h2></div>
        <Loading q={hist}>{rows => <>{rows.map(h => <div key={h.id} className="list-i"><span className="small muted" style={{ minWidth: 110 }}>{fmtDateTime(h.at)}</span><div className="small"><b>{h.actor}</b> · {h.text}</div></div>)}
          {rows.length === 0 && <p className="empty">No renewals recorded yet.</p>}</>}</Loading>
      </section>
      {renew && <RenewModal d={renew} onClose={() => setRenew(null)} />}
    </>
  );
}

function RenewModal({ d, onClose }: { d: Doc; onClose: () => void }) {
  const toast = useToast();
  const [date, setDate] = useState(new Date(Date.now() + 365 * 864e5).toISOString().slice(0, 10));
  const [ref, setRef] = useState('');
  const save = useAction(() => api(`/compliance/${d.id}/renew`, { body: { expiresOn: date, reference: ref } }), () => { toast(`${d.subjectId} ${d.docType} renewed`); onClose(); });
  return <Modal title={`Renew ${d.docType} · ${d.subjectId}`} onClose={onClose}>
    <label className="fl">New expiry date<input className="inp" type="date" value={date} onChange={e => setDate(e.target.value)} /></label>
    <label className="fl">Certificate / policy number<input className="inp mono" value={ref} onChange={e => setRef(e.target.value)} /></label>
    <ErrorLine error={save.error} />
    <div className="actions"><button className="btn" onClick={onClose}>Cancel</button><button className="btn pri" disabled={save.isPending} onClick={() => save.mutate()}>Save renewal</button></div>
  </Modal>;
}

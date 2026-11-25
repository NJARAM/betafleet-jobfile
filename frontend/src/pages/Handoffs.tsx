import { useState } from 'react';
import { api, useAction, useApi } from '../api';
import { ErrorLine, JobLink, Loading, PageTop, Pill, Seg, UNIT_NAMES, Unit, fmtDateTime, useToast } from '../ui';
import type { Handoff } from '../types';

export default function Handoffs() {
  const [unit, setUnit] = useState('');
  const q = useApi<Handoff[]>(`/handoffs${unit ? `?unit=${unit}` : ''}`);
  const toast = useToast();
  const accept = useAction((h: Handoff) => api(`/handoffs/${h.id}/accept`, { method: 'POST' }).then(() => h),
    h => toast(`Accepted · ${h.jobId} is now with ${UNIT_NAMES[h.unit]}`));
  return (
    <>
      <PageTop crumb="Job files / Hand-over queue" title="Hand-over queue" sub="Work passed between units. Accepting a hand-over makes your unit the owner of that leg." />
      <section className="card tight">
        <div className="card-h">
          <Seg label="Receiving unit" value={unit} onChange={setUnit} options={[['', 'All'], ['C', 'Clearing'], ['W', 'Warehousing'], ['D', 'Distribution'], ['T', 'Transit'], ['B', 'Billing']]} />
        </div>
        <ErrorLine error={accept.error} />
        <Loading q={q}>{rows => <div className="scroll"><table style={{ minWidth: 760 }}>
          <thead><tr><th>To</th><th>From</th><th>Hand-over</th><th>Job file</th><th>Raised</th><th>State</th><th /></tr></thead>
          <tbody>
            {rows.map(h => <tr key={h.id}>
              <td><Unit u={h.unit} /></td><td><Unit u={h.fromUnit} /></td><td><b>{h.title}</b><div className="muted small">{h.note}</div></td>
              <td><JobLink id={h.jobId} /></td><td className="small">{fmtDateTime(h.createdAt)}</td>
              <td><Pill s={h.state} />{h.acceptedBy && <div className="muted small">{h.acceptedBy}</div>}</td>
              <td>{h.state === 'Waiting' && <button className="btn sm pri" disabled={accept.isPending} onClick={() => accept.mutate(h)}>Accept</button>}</td>
            </tr>)}
            {rows.length === 0 && <tr><td colSpan={7} className="empty">Nothing waiting for this unit.</td></tr>}
          </tbody></table></div>}</Loading>
      </section>
    </>
  );
}

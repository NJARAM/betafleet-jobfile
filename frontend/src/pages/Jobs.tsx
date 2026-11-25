import { useDeferredValue, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { useApi } from '../api';
import { Bar, Chips, JobLink, Loading, PageTop, Pill, Seg } from '../ui';
import type { JobRow } from '../types';

export default function Jobs() {
  const [unit, setUnit] = useState('');
  const [search, setSearch] = useState('');
  const q = useDeferredValue(search);
  const params = new URLSearchParams();
  if (unit) params.set('unit', unit);
  if (q.trim()) params.set('q', q.trim());
  const jobs = useApi<JobRow[]>(`/jobs?${params}`);
  const nav = useNavigate();
  return (
    <>
      <PageTop crumb="Job files" title="Job files" sub="One file number per customer consignment, whichever units it passes through."
        right={<Link className="btn pri" to="/new-job">+ Open job file</Link>} />
      <section className="card tight">
        <div className="card-h">
          <Seg label="Filter by unit" value={unit} onChange={setUnit} options={[['', 'All units'], ['C', 'Clearing'], ['W', 'Warehousing'], ['D', 'Distribution'], ['T', 'Transit']]} />
          <input className="inp" style={{ maxWidth: 360 }} aria-label="Search files" placeholder="Search file, customer, BL or UCR" value={search} onChange={e => setSearch(e.target.value)} />
        </div>
        <Loading q={jobs}>{rows => (
          <div className="scroll"><table style={{ minWidth: 960 }}>
            <thead><tr><th>Job file</th><th>Customer &amp; cargo</th><th>Reference</th><th>Units</th><th>Stage</th><th>Progress</th><th>ETA</th><th>Owner</th></tr></thead>
            <tbody>
              {rows.map(j => <tr key={j.id} className="click" onClick={() => nav(`/jobs/${j.id}`)}>
                <td><JobLink id={j.id} /></td><td><b>{j.customer}</b><div className="muted small">{j.cargo}</div></td>
                <td className="mono small">{j.reference}</td><td><Chips units={j.units} all /></td>
                <td><Pill s={j.stage} />{j.alert && <div className="small" style={{ color: 'var(--bad)', marginTop: 4 }}>{j.alert}</div>}</td>
                <td><Bar pct={j.progressPct} /></td><td className="small">{j.eta}</td><td className="small">{j.owner}</td>
              </tr>)}
              {rows.length === 0 && <tr><td colSpan={8} className="empty">No files match. Clear the search or pick another unit.</td></tr>}
            </tbody>
          </table></div>
        )}</Loading>
      </section>
      <p className="muted small" style={{ margin: 0 }}>C Clearing &amp; Forwarding · W Warehousing · D Distribution &amp; Courier · T Transit Transport. A dashed letter means the file does not use that unit.</p>
    </>
  );
}

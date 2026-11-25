import { Link, useNavigate } from 'react-router';
import { useApi } from '../api';
import { Bar, Chips, JobLink, Kpi, Loading, PageTop, Pill, Unit, fmtDateTime, money } from '../ui';
import type { Handoff, JobRow } from '../types';

type Dash = {
  stages: { stage: string; count: number }[]; openJobs: number; exceptions: { id: string; customer: string; alert: string }[];
  handoffsWaiting: number; readyToInvoice: number; fmPending: number; fmApproved: number; expiredDocs: number; expensesPending: number;
  expensesToPay: number; openJobCards: number; activeJobs: JobRow[];
  alerts: { id: number; severity: string; kind: string; text: string; shipmentOrderId: string; jobId: string; at: string }[]; handoffs: Handoff[];
};

export default function Dashboard() {
  const q = useApi<Dash>('/dashboard');
  const nav = useNavigate();
  return (
    <>
      <PageTop crumb="Job files / Control tower" title="Control tower" sub="Every job file across clearing, warehousing, distribution and transit, in one view."
        right={<Link className="btn pri" to="/new-job">+ Open job file</Link>} />
      <Loading q={q}>{d => <>
        <section className="hero">
          <div><div className="k">ONE FILE · FOUR BUSINESS UNITS</div><h2>Every file tracked from port to proof of delivery.</h2>
            <p>A job file is opened once. Each unit works its leg, and costs, documents and milestones roll up to the same file.</p></div>
          <div className="flow" aria-label="Files by stage">
            {d.stages.map((s, i) => <span key={s.stage} className="row" style={{ gap: 8 }}>{i > 0 && <span className="ar">→</span>}<div className="st">{s.stage}<b>{s.count}</b></div></span>)}
          </div>
        </section>
        <div className="kpis">
          <Kpi label="Open job files" value={d.openJobs} sub="across 4 units" />
          <Kpi label="Files with exceptions" value={d.exceptions.length} sub="need action today" tone="bad" />
          <Kpi label="Hand-overs waiting" value={d.handoffsWaiting} sub="between units" tone="warn" />
          <Kpi label="Ready to invoice" value={money(d.readyToInvoice)} sub="ready lines, not billed" tone="ok" />
          <Kpi label="Filling masters to approve" value={d.fmPending} sub="Ops manager" tone="warn" />
          <Kpi label="Approved · to dispatch" value={d.fmApproved} sub="filling masters" />
          <Kpi label="Expired documents" value={d.expiredDocs} sub="trucks & drivers blocked" tone="bad" />
          <Kpi label="Expenses awaiting approval" value={d.expensesPending} sub={`${d.expensesToPay} approved, to pay`} tone="warn" />
        </div>
        <div className="grid2">
          <section className="card tight">
            <div className="card-h"><div><h2>Active job files</h2><div className="muted small">Each file shows which units are involved</div></div><Link className="btn sm" to="/jobs">All files</Link></div>
            <div className="scroll"><table style={{ minWidth: 720 }}><thead><tr><th>Job file</th><th>Customer</th><th>Units</th><th>Stage</th><th>Progress</th></tr></thead><tbody>
              {d.activeJobs.map(j => <tr key={j.id} className="click" onClick={() => nav(`/jobs/${j.id}`)}>
                <td><JobLink id={j.id} /></td><td><b>{j.customer}</b><div className="muted small">{j.cargo}</div></td>
                <td><Chips units={j.units} all /></td><td><Pill s={j.stage} /></td><td><Bar pct={j.progressPct} /></td></tr>)}
            </tbody></table></div>
          </section>
          <div className="stack">
            <section className="card tight">
              <div className="card-h"><h2>Exceptions</h2><span className="pill p-bad">{d.exceptions.length}</span></div>
              {d.exceptions.map(e => <div key={e.id} className="list-i"><span className="pill p-bad">!</span><div><b>{e.alert}</b><div className="muted small"><Link to={`/jobs/${e.id}`}>{e.id}</Link> · {e.customer}</div></div></div>)}
            </section>
            <section className="card tight">
              <div className="card-h"><h2>Live GPS alerts</h2><Link className="btn sm" to="/tracking">Tracking</Link></div>
              {d.alerts.map(a => <div key={a.id} className="list-i"><span className={`pill p-${a.severity}`}>{a.kind}</span><div><b className="small">{a.text}</b><div className="muted small">{a.shipmentOrderId} · <Link to={`/jobs/${a.jobId}`}>{a.jobId}</Link> · {fmtDateTime(a.at)}</div></div></div>)}
            </section>
            <section className="card tight">
              <div className="card-h"><h2>Waiting hand-overs</h2><Link className="btn sm" to="/handoffs">Open queue</Link></div>
              {d.handoffs.map(h => <div key={h.id} className="list-i"><Unit u={h.unit} /><div><b>{h.title}</b><div className="muted small">{h.jobId}</div></div></div>)}
            </section>
          </div>
        </div>
      </>}</Loading>
    </>
  );
}

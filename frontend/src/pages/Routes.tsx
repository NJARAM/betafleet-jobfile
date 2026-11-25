import { useState } from 'react';
import { useApi } from '../api';
import { Loading, PageTop, fmtDate, money } from '../ui';

type R = {
  route: { code: string; name: string; via: string; outKm: number; returnKm: number; days: number; type: string; vehicleClass: string; toll: number; borders: number; escort: boolean; effectiveFrom: string; reviewOn: string; version: number };
  lines: { code: string; name: string; basis: string; qty: number; rate: number; unit: string; standard: number }[]; total: number;
};

export default function RoutesPage() {
  const q = useApi<R[]>('/routes');
  const [sel, setSel] = useState(0);
  return (
    <>
      <PageTop crumb="Transit Transport" title="Route master" sub="Standard trip costs per route. Every filling master starts from these lines." />
      <Loading q={q}>{rows => {
        const r = rows[sel] ?? rows[0];
        return <div className="grid2 g-list">
          <section className="card tight"><div className="listsel">{rows.map((x, i) => <button key={x.route.code} className={i === sel ? 'on' : ''} onClick={() => setSel(i)}>
            <b className="mono">{x.route.code}</b><span className="small">{x.route.name}</span><span className="small muted">{x.route.outKm.toLocaleString()} km · {money(x.total, 2)} / truck</span>
          </button>)}</div></section>
          {r && <section className="card tight">
            <div className="card-h"><div><h2>{r.route.name} <span className="muted mono small">v{r.route.version}</span></h2><div className="muted small">{r.route.via}</div></div></div>
            <div className="tabbody">
              <dl className="kv">
                <dt>Type</dt><dd>{r.route.type}</dd><dt>Vehicle class</dt><dd>{r.route.vehicleClass}</dd>
                <dt>Distance</dt><dd>{r.route.outKm.toLocaleString()} km out · {r.route.returnKm.toLocaleString()} km return · {r.route.days} days</dd>
                <dt>Borders / escort</dt><dd>{r.route.borders} crossing(s) · {r.route.escort ? 'Escort mandatory' : 'No escort'}</dd>
                <dt>Effective</dt><dd>{fmtDate(r.route.effectiveFrom)} → review {fmtDate(r.route.reviewOn)}</dd>
              </dl>
            </div>
            <div className="scroll"><table><thead><tr><th>Code</th><th>Line</th><th>Basis</th><th className="r">Qty</th><th className="r">Rate</th><th className="r">Standard</th></tr></thead><tbody>
              {r.lines.map(l => <tr key={l.code}><td className="mono small">{l.code}</td><td>{l.name}</td><td className="small muted">{l.basis}</td><td className="r num">{l.qty} {l.unit}</td><td className="r num">{money(l.rate, 2)}</td><td className="r num">{money(l.standard, 2)}</td></tr>)}
              <tr><td colSpan={5}><b>Standard per truck</b></td><td className="r num"><b>{money(r.total, 2)}</b></td></tr>
            </tbody></table></div>
          </section>}
        </div>;
      }}</Loading>
    </>
  );
}

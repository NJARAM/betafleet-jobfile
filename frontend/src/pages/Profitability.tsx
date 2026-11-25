import { useState } from 'react';
import { useApi } from '../api';
import { JobLink, Loading, PageTop, Pill, Seg, Unit, money } from '../ui';

type Row = { revenue: number; cost: number; margin: number; marginPct?: number };
type P = {
  jobs: (Row & { id: string; customer: string; stage: string; units: (Row & { unit: string })[] })[];
  units: (Row & { unit: string; name: string })[];
  customers: (Row & { customer: string; jobs: number })[];
};

const pct = (r: Row) => r.revenue ? Math.round((r.margin / r.revenue) * 1000) / 10 : 0;
const tone = (p: number) => p < 0 ? 'var(--bad)' : p < 15 ? 'var(--warn)' : 'var(--ok)';

export default function Profitability() {
  const q = useApi<P>('/profitability');
  const [view, setView] = useState<'job' | 'unit' | 'customer'>('job');
  return (
    <>
      <PageTop crumb="Finance" title="Profitability" sub="Revenue against cost per job file, business unit and customer. Cost = charge costs + approved job expenses + disbursed trip funds." />
      <Loading q={q}>{d => {
        const tot = d.units.reduce((a, u) => ({ revenue: a.revenue + u.revenue, cost: a.cost + u.cost, margin: a.margin + u.margin }), { revenue: 0, cost: 0, margin: 0 });
        const max = Math.max(1, ...d.units.map(u => Math.max(u.revenue, u.cost)));
        return <>
          <div className="kpis">
            <div className="kpi"><div><div className="l">Revenue</div><div className="v num">{money(tot.revenue)}</div></div></div>
            <div className="kpi"><div><div className="l">Cost</div><div className="v num">{money(tot.cost)}</div></div></div>
            <div className="kpi"><div><div className="l">Margin</div><div className="v num" style={{ color: tone(pct(tot)) }}>{money(tot.margin)}</div><div className="s">{pct(tot)}% of revenue</div></div></div>
          </div>
          <section className="card stack">
            <h2>By business unit</h2>
            {d.units.map(u => <div key={u.unit} className="row" style={{ flexWrap: 'nowrap', alignItems: 'center' }}>
              <span style={{ width: 150 }} className="row"><Unit u={u.unit} /><span className="small">{u.name.split(' ')[0]}</span></span>
              <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 3 }}>
                <div style={{ height: 10, width: `${u.revenue / max * 100}%`, background: `var(--${u.unit.toLowerCase()})`, borderRadius: 3 }} title={`Revenue ${money(u.revenue)}`} />
                <div style={{ height: 10, width: `${u.cost / max * 100}%`, background: '#b8c4d1', borderRadius: 3 }} title={`Cost ${money(u.cost)}`} />
              </div>
              <span className="num small" style={{ width: 170, textAlign: 'right' }}>{money(u.revenue)} / {money(u.cost)} · <b style={{ color: tone(pct(u)) }}>{pct(u)}%</b></span>
            </div>)}
            <p className="muted small" style={{ margin: 0 }}>Coloured bar = revenue, grey bar = cost.</p>
          </section>
          <section className="card tight">
            <div className="card-h"><Seg label="Breakdown" value={view} onChange={setView} options={[['job', 'By job file'], ['customer', 'By customer'], ['unit', 'By unit']]} /></div>
            <div className="scroll"><table style={{ minWidth: 720 }}>
              {view === 'job' && <><thead><tr><th>Job file</th><th>Customer</th><th>Stage</th><th className="r">Revenue</th><th className="r">Cost</th><th className="r">Margin</th><th className="r">%</th></tr></thead>
                <tbody>{d.jobs.map(j => <tr key={j.id}><td><JobLink id={j.id} /></td><td>{j.customer}</td><td><Pill s={j.stage} /></td><td className="r num">{money(j.revenue)}</td><td className="r num">{money(j.cost)}</td><td className="r num">{money(j.margin)}</td><td className="r num" style={{ color: tone(pct(j)) }}>{pct(j)}%</td></tr>)}</tbody></>}
              {view === 'customer' && <><thead><tr><th>Customer</th><th className="r">Files</th><th className="r">Revenue</th><th className="r">Cost</th><th className="r">Margin</th><th className="r">%</th></tr></thead>
                <tbody>{d.customers.map(c => <tr key={c.customer}><td>{c.customer}</td><td className="r num">{c.jobs}</td><td className="r num">{money(c.revenue)}</td><td className="r num">{money(c.cost)}</td><td className="r num">{money(c.margin)}</td><td className="r num" style={{ color: tone(pct(c)) }}>{pct(c)}%</td></tr>)}</tbody></>}
              {view === 'unit' && <><thead><tr><th>Unit</th><th className="r">Revenue</th><th className="r">Cost</th><th className="r">Margin</th><th className="r">%</th></tr></thead>
                <tbody>{d.units.map(u => <tr key={u.unit}><td><Unit u={u.unit} /> {u.name}</td><td className="r num">{money(u.revenue)}</td><td className="r num">{money(u.cost)}</td><td className="r num">{money(u.margin)}</td><td className="r num" style={{ color: tone(pct(u)) }}>{pct(u)}%</td></tr>)}</tbody></>}
            </table></div>
          </section>
        </>;
      }}</Loading>
    </>
  );
}

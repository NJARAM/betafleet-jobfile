import { useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router';
import { api, useAction, useApi } from '../api';
import { ErrorLine, Loading, ORDER, PageTop, UNIT_NAMES, UNIT_SHORT, Unit, useToast } from '../ui';
import type { Lookups } from '../types';

export default function NewJob() {
  const lk = useApi<Lookups>('/lookups');
  return <Loading q={lk}>{l => <Wizard l={l} />}</Loading>;
}

function Wizard({ l }: { l: Lookups }) {
  const nav = useNavigate();
  const toast = useToast();
  const [f, setF] = useState({
    customerId: l.customers[0]?.id ?? 0, direction: 'Import', reference: '', cargo: '', cargoType: 'Containerised', quotation: '', eta: '',
    units: { C: true, W: false, D: false, T: true } as Record<string, boolean>,
    entry: l.entryTypes[0], warehouse: l.warehouses[0], storageDays: 10, drops: 1, route: l.routes[0]?.name ?? '', trucks: 1
  });
  const set = (k: string, v: unknown) => setF(x => ({ ...x, [k]: v }));
  const units = ORDER.filter(u => f.units[u]);
  const create = useAction(() => api<{ id: string }>('/jobs', { body: { ...f, units, quotation: f.quotation || null, eta: f.eta || null } }),
    r => { toast(`${r.id} opened · ${units.length} hand-over(s) sent`); nav(`/jobs/${r.id}`); });

  const box = (k: string, body: ReactNode) => (
    <div className={`sp ${f.units[k] ? 'on' : ''}`}>
      <label className="hd"><input type="checkbox" checked={f.units[k]} onChange={e => set('units', { ...f.units, [k]: e.target.checked })} /><Unit u={k} />{UNIT_NAMES[k]}</label>
      {f.units[k] ? body : <span className="muted small">Not part of this file. Can be added later.</span>}
    </div>
  );
  const sel = (k: string, label: string, opts: string[]) => <label className="fl">{label}<select className="inp" value={String((f as Record<string, unknown>)[k])} onChange={e => set(k, e.target.value)}>{opts.map(o => <option key={o}>{o}</option>)}</select></label>;
  const num = (k: string, label: string) => <label className="fl">{label}<input className="inp num" inputMode="numeric" value={String((f as Record<string, unknown>)[k])} onChange={e => set(k, Math.max(0, parseInt(e.target.value) || 0))} /></label>;

  return (
    <>
      <PageTop crumb="Job files / Open job file" title="Open job file" sub="Capture the consignment once, tick the units it needs, and each unit receives its hand-over." />
      <div className="grid2">
        <div className="stack">
          <section className="card stack">
            <div className="eyebrow">1 · Customer &amp; consignment</div>
            <div className="fgrid">
              <label className="fl">Customer<select className="inp" value={f.customerId} onChange={e => set('customerId', +e.target.value)}>{l.customers.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
              <label className="fl">Quotation (optional)<input className="inp mono" placeholder="QT-2026-0318" value={f.quotation} onChange={e => set('quotation', e.target.value)} /></label>
              {sel('direction', 'Movement', ['Import', 'Export', 'Transit', 'Local'])}
              <label className="fl">BL / AWB / UCR<input className="inp mono" value={f.reference} onChange={e => set('reference', e.target.value)} /></label>
              <label className="fl">Cargo<input className="inp" placeholder="2 × 40′ conveyor parts" value={f.cargo} onChange={e => set('cargo', e.target.value)} /></label>
              {sel('cargoType', 'Cargo type', ['Containerised', 'Loose cargo'])}
              <label className="fl">ETA<input className="inp" type="date" value={f.eta} onChange={e => set('eta', e.target.value)} /></label>
            </div>
          </section>
          <section className="card stack">
            <div className="eyebrow">2 · Units involved</div>
            <div className="svcpick">
              {box('C', sel('entry', 'Customs entry', l.entryTypes))}
              {box('W', <>{sel('warehouse', 'Warehouse', l.warehouses)}{num('storageDays', 'Expected storage (days)')}</>)}
              {box('D', num('drops', 'Delivery points'))}
              {box('T', <>{sel('route', 'Route', l.routes.map(r => r.name))}{num('trucks', 'Trucks')}</>)}
            </div>
          </section>
        </div>
        <aside className="stack" style={{ position: 'sticky', top: 16 }}>
          <section className="card stack">
            <div className="eyebrow">File flow</div>
            <div className="fprev">{units.length ? units.map((k, i) => <span key={k} className="row" style={{ gap: 8 }}>{i > 0 && <span className="arr">→</span>}
              <div className="node" style={{ background: `var(--${k.toLowerCase()}-bg)`, color: `var(--${k.toLowerCase()})` }}>{UNIT_SHORT[k]}</div></span>) : <span className="muted">Tick at least one unit.</span>}</div>
            <div className="eyebrow">Created on save</div>
            {units.map(k => <div key={k} className="row" style={{ flexWrap: 'nowrap' }}><Unit u={k} /><span className="small">Hand-over to {UNIT_NAMES[k]}{k === 'C' ? ` · ${f.entry} entry` : k === 'T' ? ` · ${f.trucks} truck(s)` : ''}</span></div>)}
            <ErrorLine error={create.error} />
            <button className="btn pri" disabled={!units.length || create.isPending} onClick={() => create.mutate()}>Create job file</button>
          </section>
        </aside>
      </div>
    </>
  );
}

import { useState } from 'react';
import { api, useAction, useApi } from '../api';
import { ErrorLine, Loading, PageTop, Pill, Seg, Tabs, fmtDateTime, money, useToast } from '../ui';
import type { Activity } from '../types';

type Tyre = { serial: string; size: string; type: string; status: string; vehicle?: string; position?: string; treadMm: number; km: number; cost: number; retreads: number };
const LAYOUT = [['A1-L', '', '', 'A1-R'], ['A2-LO', 'A2-LI', 'A2-RI', 'A2-RO'], ['A3-LO', 'A3-LI', 'A3-RI', 'A3-RO'], ['T1-L', '', '', 'T1-R'], ['T2-L', '', '', 'T2-R'], ['T3-L', '', '', 'T3-R']];
const MIN = 4;

export default function Tyres() {
  const q = useApi<Tyre[]>('/tyres');
  const [tab, setTab] = useState<'veh' | 'store'>('veh');
  return (
    <>
      <PageTop crumb="Fleet" title="Tyre management" sub={`Every tyre by serial: where it is fitted, tread depth, km run and cost per km. Below ${MIN} mm the truck fails its fitness check.`} />
      <section className="card tight">
        <Tabs value={tab} onChange={setTab} tabs={[['veh', 'By vehicle'], ['store', 'Store & retread']]} />
        <div className="tabbody"><Loading q={q}>{rows => tab === 'veh' ? <ByVehicle rows={rows} /> : <Store rows={rows} />}</Loading></div>
      </section>
    </>
  );
}

function ByVehicle({ rows }: { rows: Tyre[] }) {
  const vehicles = [...new Set(rows.filter(r => r.vehicle).map(r => r.vehicle!))];
  const [veh, setVeh] = useState(vehicles.find(v => rows.some(r => r.vehicle === v && r.treadMm < MIN)) ?? vehicles[0]);
  const [pos, setPos] = useState<string | null>(null);
  const fitted = rows.filter(r => r.vehicle === veh);
  const t = fitted.find(r => r.position === pos);
  return <div className="grid2">
    <div className="stack">
      <Seg label="Vehicle" value={veh} onChange={v => { setVeh(v); setPos(null); }} options={vehicles.map(v => [v, v + (rows.some(r => r.vehicle === v && r.treadMm < MIN) ? ' ⚠' : '')])} />
      <div className="tyremap" aria-label="Axle layout">
        {LAYOUT.flat().map((p, i) => {
          if (!p) return <span key={i} />;
          const x = fitted.find(r => r.position === p);
          return <button key={p} className={`tyre${x && x.treadMm < MIN ? ' low' : ''}${pos === p ? ' on' : ''}`} onClick={() => setPos(p)} title={p}>{x ? x.treadMm : '—'}<small>{p}</small></button>;
        })}
      </div>
    </div>
    <div>{t ? <TyreCard t={t} store={rows.filter(r => r.status.startsWith('In store') && r.size === t.size)} /> : pos ? <EmptyPos veh={veh} pos={pos} store={rows.filter(r => r.status.startsWith('In store'))} /> : <p className="muted">Pick a wheel position.</p>}</div>
  </div>;
}

function useTyreAction() {
  const toast = useToast();
  return useAction((v: { serial: string; action: string; vehicle?: string; position?: string; treadMm?: number; reason?: string }) => api(`/tyres/${v.serial}/action`, { body: v }).then(() => v), v => toast(`${v.serial} · ${v.action}`));
}

function TyreCard({ t, store }: { t: Tyre; store: Tyre[] }) {
  const act = useTyreAction();
  const hist = useApi<Activity[]>(`/tyres/${t.serial}/history`);
  const [mm, setMm] = useState(String(t.treadMm));
  const [reason, setReason] = useState('');
  return <section className="card stack">
    <div className="row" style={{ justifyContent: 'space-between' }}><h3 className="mono">{t.serial}</h3><Pill s={t.treadMm < MIN ? 'Below minimum' : 'OK'} kind={t.treadMm < MIN ? 'p-bad' : 'p-ok'} /></div>
    <dl className="kv"><dt>Position</dt><dd>{t.vehicle} · {t.position}</dd><dt>Size · type</dt><dd>{t.size} · {t.type}</dd><dt>Tread</dt><dd>{t.treadMm} mm</dd>
      <dt>Km run</dt><dd>{t.km.toLocaleString()} km</dd><dt>Cost / km</dt><dd>{t.km ? money(t.cost / t.km * 1000, 2) + ' per 1,000 km' : '—'}</dd><dt>Retreads</dt><dd>{t.retreads}</dd></dl>
    <ErrorLine error={act.error} />
    <div className="row"><input className="inp sm num" style={{ width: 80 }} value={mm} onChange={e => setMm(e.target.value)} aria-label="Tread mm" />
      <button className="btn sm" onClick={() => act.mutate({ serial: t.serial, action: 'measure', treadMm: parseFloat(mm) })}>Record tread</button></div>
    <div className="row"><input className="inp sm" style={{ maxWidth: 240 }} placeholder="Reason for removal" value={reason} onChange={e => setReason(e.target.value)} />
      <button className="btn sm bad" onClick={() => act.mutate({ serial: t.serial, action: 'remove', reason })}>Remove</button></div>
    <p className="muted small" style={{ margin: 0 }}>{store.length} matching tyre(s) in store to replace it.</p>
    <div>{(hist.data ?? []).map(h => <div key={h.id} className="small"><span className="muted">{fmtDateTime(h.at)}</span> · {h.text}</div>)}</div>
  </section>;
}

function EmptyPos({ veh, pos, store }: { veh: string; pos: string; store: Tyre[] }) {
  const act = useTyreAction();
  const size = pos.startsWith('A1') ? '385/65R22.5' : '315/80R22.5';
  const fits = store.filter(s => s.size === size);
  const [serial, setSerial] = useState(fits[0]?.serial ?? '');
  return <section className="card stack">
    <h3>{veh} · {pos} is empty</h3>
    <ErrorLine error={act.error} />
    {fits.length ? <div className="row"><select className="inp" style={{ maxWidth: 300 }} value={serial} onChange={e => setSerial(e.target.value)}>{fits.map(s => <option key={s.serial} value={s.serial}>{s.serial} · {s.status} · {s.treadMm} mm</option>)}</select>
      <button className="btn pri" onClick={() => act.mutate({ serial, action: 'fit', vehicle: veh, position: pos })}>Fit tyre</button></div> : <p className="muted">No {size} in store.</p>}
  </section>;
}

function Store({ rows }: { rows: Tyre[] }) {
  const act = useTyreAction();
  const list = rows.filter(r => r.status !== 'Fitted');
  return <>
    <ErrorLine error={act.error} />
    <table><thead><tr><th>Serial</th><th>Size</th><th>Status</th><th className="r">Tread</th><th className="r">Km run</th><th className="r">Retreads</th><th /></tr></thead><tbody>
      {list.map(t => <tr key={t.serial}><td className="mono">{t.serial}</td><td className="small">{t.size}</td><td><Pill s={t.status} /></td><td className="r num">{t.treadMm} mm</td><td className="r num">{t.km.toLocaleString()}</td><td className="r num">{t.retreads}</td>
        <td><div className="row" style={{ flexWrap: 'nowrap' }}>
          {(t.status === 'Awaiting retread' || t.status === 'In store · used') && <button className="btn sm" onClick={() => act.mutate({ serial: t.serial, action: 'retread' })}>Send to retreader</button>}
          {t.status === 'At retreader' && <button className="btn sm" onClick={() => act.mutate({ serial: t.serial, action: 'receive-retread' })}>Receive retreaded</button>}
          {t.status !== 'Scrapped' && <button className="btn sm bad" onClick={() => act.mutate({ serial: t.serial, action: 'scrap', reason: 'Casing not repairable' })}>Scrap</button>}
        </div></td></tr>)}
    </tbody></table>
  </>;
}

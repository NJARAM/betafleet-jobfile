import { useState } from 'react';
import { api, useAction, useApi } from '../api';
import { useSession } from '../session';
import { ErrorLine, Loading, PageTop, Pill, useToast } from '../ui';
import type { Lookups, Shipment } from '../types';
import { PodModal } from './Tracking';

const STOPS: [string, number][] = [['Dar es Salaam', 0], ['Morogoro', 10], ['Iringa', 26], ['Mbeya', 43], ['Tunduma / Nakonde', 48], ['Mpika', 70], ['Kapiri Mposhi', 90], ['Lusaka', 99]];

export default function DriverApp() {
  const me = useSession().user!;
  const isDriver = me.role === 'driver';
  // Drivers see only their own trips (the API forces this); staff can preview any driver's screen.
  const lk = useApi<Lookups>(isDriver ? null : '/lookups').data;
  const [picked, setDriver] = useState('P. Banda');
  const driver = isDriver ? me.displayName : picked;
  const q = useApi<Shipment[]>(`/shipments?driver=${encodeURIComponent(driver)}`);
  return (
    <>
      <PageTop crumb="Fleet" title="Driver app" sub="What the driver sees on the phone: today's trip, check-ins along the corridor, and POD capture." />
      {!isDriver && <label className="fl" style={{ maxWidth: 300 }}>Driver<select className="inp" value={driver} onChange={e => setDriver(e.target.value)}>{lk?.drivers.map(d => <option key={d}>{d}</option>)}</select></label>}
      <div className="phone">
        <div className="ph"><b>BetaFleet Driver</b><div className="small" style={{ opacity: .8 }}>{driver}</div></div>
        <div className="pb"><Loading q={q}>{rows => {
          const trip = rows.find(s => !s.deliveredAt);
          return trip ? <Trip s={trip} /> : <p className="muted">No active trip. {rows.length ? `Last: ${rows[0].id} delivered.` : ''}</p>;
        }}</Loading></div>
      </div>
    </>
  );
}

function Trip({ s }: { s: Shipment }) {
  const toast = useToast();
  const [pod, setPod] = useState(false);
  const next = STOPS.find(([, p]) => p > s.progressPct);
  const checkin = useAction((v: { place: string; pct: number; speed: number }) => api(`/shipments/${s.id}/position`, { method: 'PUT', body: { progressPct: v.pct, place: v.place, speedKmh: v.speed } }).then(() => v), v => toast(`Checked in at ${v.place}`));
  return <>
    <div className="card stack" style={{ padding: 14 }}>
      <div className="row" style={{ justifyContent: 'space-between' }}><b className="mono">{s.id}</b><Pill s={s.status} /></div>
      <div className="small">{s.truck} · job {s.jobId}</div>
      <div className="small">Now: <b>{s.place}</b> · {s.progressPct}% · ETA {s.eta}</div>
      <div className="bar"><span style={{ width: `${s.progressPct}%` }} /></div>
    </div>
    <ErrorLine error={checkin.error} />
    {next && <button className="btn pri" disabled={checkin.isPending} onClick={() => checkin.mutate({ place: next[0], pct: next[1], speed: 0 })}>Check in at {next[0]}</button>}
    <button className="btn" disabled={checkin.isPending} onClick={() => checkin.mutate({ place: s.place, pct: s.progressPct, speed: 0 })}>Report stop / delay</button>
    <button className="btn dark" onClick={() => setPod(true)}>Delivered · capture POD</button>
    {pod && <PodModal s={s} onClose={() => setPod(false)} />}
  </>;
}

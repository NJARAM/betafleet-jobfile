import { useState } from 'react';
import { api, useAction, useApi } from '../api';
import { ErrorLine, JobLink, Loading, Modal, PageTop, Pill, fmtDateTime, useToast } from '../ui';
import type { Shipment } from '../types';

const STOPS: [string, number][] = [['Dar es Salaam', 0], ['Morogoro', 10], ['Iringa', 26], ['Mbeya', 43], ['Tunduma / Nakonde', 48], ['Mpika', 70], ['Kapiri Mposhi', 90], ['Lusaka', 100]];
type Alert = { id: number; severity: string; kind: string; text: string; shipmentOrderId: string; jobId: string; at: string };

export default function Tracking() {
  const so = useApi<Shipment[]>('/shipments');
  const alerts = useApi<Alert[]>('/tracking/alerts');
  const [pod, setPod] = useState<Shipment | null>(null);
  return (
    <>
      <PageTop crumb="Transit Transport" title="Shipments & live tracking" sub="Shipment orders on the corridor, with GPS position, speed and exceptions." />
      <Loading q={so}>{rows => {
        const live = rows.filter(s => !s.deliveredAt);
        return <>
          <section className="card stack">
            <h2>Dar es Salaam → Lusaka corridor</h2>
            <div style={{ position: 'relative', padding: '18px 8px 34px' }}>
              <div className="track">
                {live.filter(s => s.routeCode === 'DSM-LUN-TUN').map(s => <span key={s.id} title={`${s.truck} · ${s.place}`} style={{ left: `${s.progressPct}%`, color: s.status === 'Stopped' ? 'var(--bad)' : s.status === 'Speeding' ? 'var(--warn)' : 'var(--info)' }} />)}
              </div>
              {STOPS.map(([n, p]) => <span key={n} className="small muted" style={{ position: 'absolute', left: `calc(${p}% + 8px)`, top: 36, transform: 'translateX(-50%)', whiteSpace: 'nowrap', fontSize: 10.5 }}>{n}</span>)}
            </div>
          </section>
          <section className="card tight">
            <div className="card-h"><h2>Shipment orders</h2><span className="muted small">{live.length} on the road</span></div>
            <div className="scroll"><table style={{ minWidth: 980 }}>
              <thead><tr><th>Shipment order</th><th>Job file</th><th>Truck &amp; driver</th><th>Position</th><th className="r">GPS km / std</th><th>ETA</th><th>Status</th><th /></tr></thead>
              <tbody>{rows.map(s => <tr key={s.id}>
                <td className="mono small"><b>{s.id}</b><div className="muted">{s.fillingMasterId ?? '—'}</div></td><td><JobLink id={s.jobId} /></td>
                <td>{s.truck}<div className="muted small">{s.driver}</div></td>
                <td>{s.place}<div className="muted small">{s.progressPct}% · {s.speedKmh} km/h</div></td>
                <td className="r num small">{s.gpsKm.toLocaleString()} / {s.standardKm.toLocaleString()}</td>
                <td className="small">{s.deliveredAt ? fmtDateTime(s.deliveredAt) : s.eta}</td>
                <td><Pill s={s.status} />{s.podSignedBy && <div className="muted small">POD: {s.podSignedBy}</div>}</td>
                <td>{!s.deliveredAt && <button className="btn sm" onClick={() => setPod(s)}>Capture POD</button>}</td>
              </tr>)}</tbody></table></div>
          </section>
        </>;
      }}</Loading>
      <section className="card tight">
        <div className="card-h"><h2>GPS alerts</h2></div>
        <Loading q={alerts}>{rows => <>{rows.map(a => <div key={a.id} className="list-i"><span className={`pill p-${a.severity}`}>{a.kind}</span>
          <div><b className="small">{a.text}</b><div className="muted small">{a.shipmentOrderId} · <JobLink id={a.jobId} /> · {fmtDateTime(a.at)}</div></div></div>)}</>}</Loading>
      </section>
      {pod && <PodModal s={pod} onClose={() => setPod(null)} />}
    </>
  );
}

export function PodModal({ s, onClose }: { s: Shipment; onClose: () => void }) {
  const [by, setBy] = useState('');
  const toast = useToast();
  const save = useAction(() => api(`/shipments/${s.id}/pod`, { body: { signedBy: by } }), () => { toast(`${s.id} delivered · POD captured`); onClose(); });
  return (
    <Modal title={`Proof of delivery · ${s.id}`} onClose={onClose}>
      <p className="muted" style={{ margin: 0 }}>{s.truck} · {s.driver}. When the last truck on a job is delivered, its freight lines become ready to invoice.</p>
      <label className="fl">Received and signed by<input className="inp" autoFocus placeholder="Name, company" value={by} onChange={e => setBy(e.target.value)} /></label>
      <ErrorLine error={save.error} />
      <div className="actions"><button className="btn" onClick={onClose}>Cancel</button><button className="btn pri" disabled={!by.trim() || save.isPending} onClick={() => save.mutate()}>Confirm delivery</button></div>
    </Modal>
  );
}

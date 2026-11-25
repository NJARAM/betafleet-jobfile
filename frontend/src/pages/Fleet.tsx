import { useState } from 'react';
import { Link } from 'react-router';
import { useApi } from '../api';
import { JobLink, Loading, PageTop, Pill, Tabs } from '../ui';

type Veh = { reg: string; fleetNo: string; type: string; status: string; driver?: string; trailer?: string; odometerKm?: number; nextServiceKm?: number; subcontracted: boolean;
  trip?: { id: string; jobId: string }; openJobCard: boolean; expiredDocs: number; lowTyres: number };
type Drv = { name: string; note: string; expired: number; expiring: number };

export default function Fleet() {
  const v = useApi<Veh[]>('/fleet/vehicles');
  const d = useApi<Drv[]>('/fleet/drivers');
  const [tab, setTab] = useState<'veh' | 'drv'>('veh');
  return (
    <>
      <PageTop crumb="Fleet" title="Fleet register" sub="Trucks, trailers and drivers, with what is blocking each one from the road." />
      <section className="card tight">
        <Tabs value={tab} onChange={setTab} tabs={[['veh', 'Vehicles'], ['drv', 'Drivers']]} />
        {tab === 'veh' && <Loading q={v}>{rows => <div className="scroll"><table style={{ minWidth: 980 }}>
          <thead><tr><th>Vehicle</th><th>Type</th><th>Status</th><th>Driver · trailer</th><th>Current work</th><th className="r">Odometer</th><th>Blockers</th></tr></thead>
          <tbody>{rows.map(x => {
            const due = x.odometerKm && x.nextServiceKm ? x.nextServiceKm - x.odometerKm : null;
            return <tr key={x.reg}>
              <td><b>{x.reg}</b><div className="muted small mono">{x.fleetNo}</div></td><td className="small">{x.type}</td><td><Pill s={x.status} /></td>
              <td className="small">{x.driver ?? '—'}<div className="muted">{x.trailer ?? ''}</div></td>
              <td className="small">{x.trip ? <><span className="mono">{x.trip.id}</span> · <JobLink id={x.trip.jobId} /></> : '—'}</td>
              <td className="r num small">{x.odometerKm?.toLocaleString() ?? 'Owner maintained'}{due !== null && <div className="muted" style={{ color: due < 2000 ? 'var(--warn)' : undefined }}>service in {due.toLocaleString()} km</div>}</td>
              <td><div className="row" style={{ gap: 4 }}>
                {x.openJobCard && <Link to="/workshop"><Pill s="Open job card" kind="p-warn" /></Link>}
                {x.expiredDocs > 0 && <Link to="/compliance"><Pill s={`${x.expiredDocs} expired doc`} kind="p-bad" /></Link>}
                {x.lowTyres > 0 && <Link to="/tyres"><Pill s={`${x.lowTyres} low tyre`} kind="p-bad" /></Link>}
                {!x.openJobCard && !x.expiredDocs && !x.lowTyres && <Pill s="Clear" kind="p-ok" />}
              </div></td>
            </tr>;
          })}</tbody></table></div>}</Loading>}
        {tab === 'drv' && <Loading q={d}>{rows => <table><thead><tr><th>Driver</th><th>Note</th><th>Documents</th></tr></thead><tbody>
          {rows.map(x => <tr key={x.name}><td><b>{x.name}</b></td><td className="small muted">{x.note}</td>
            <td>{x.expired > 0 ? <Pill s={`${x.expired} expired`} kind="p-bad" /> : x.expiring > 0 ? <Pill s={`${x.expiring} expiring`} kind="p-warn" /> : <Pill s="Valid" />}</td></tr>)}
        </tbody></table>}</Loading>}
      </section>
    </>
  );
}

import { useState } from 'react';
import { api, useAction, useApi } from '../api';
import { ErrorLine, JobLink, Loading, Modal, PageTop, Pill, Seg, UNIT_NAMES, useToast } from '../ui';

type Entry = {
  id: string; jobId: string; customer: string; entryNo: string; entryType: string; containers: string; stage: number; stageName: string;
  nextUnit: string; handedOver: boolean; stages: string[];
};

export default function Clearing() {
  const q = useApi<Entry[]>('/clearing');
  const [f, setF] = useState<'open' | 'all'>('open');
  const [lodge, setLodge] = useState<Entry | null>(null);
  const toast = useToast();
  const adv = useAction((v: { e: Entry; entryNo?: string }) => api(`/clearing/${v.e.id}/advance`, { body: { entryNo: v.entryNo ?? null } }).then(() => v.e),
    e => { setLodge(null); toast(`${e.id} → ${e.stages[e.stage + 1]}${e.stage + 1 === 4 && e.nextUnit !== 'X' ? ` · hand-over sent to ${UNIT_NAMES[e.nextUnit]}` : ''}`); });

  return (
    <>
      <PageTop crumb="Clearing & Forwarding" title="Clearing entries" sub="Each customs entry moves through six stages. Release hands the cargo to the next unit automatically." />
      <ErrorLine error={adv.error} />
      <Loading q={q}>{rows => {
        const list = rows.filter(r => f === 'all' || r.stage < 5);
        return <section className="card tight">
          <div className="card-h"><Seg label="Filter" value={f} onChange={setF} options={[['open', 'In progress'], ['all', 'All entries']]} /><span className="muted small">{list.length} entries</span></div>
          <div className="scroll"><table style={{ minWidth: 980 }}>
            <thead><tr><th>Entry</th><th>Job file</th><th>Type &amp; containers</th><th>Stage</th><th>Next</th><th /></tr></thead>
            <tbody>{list.map(e => <tr key={e.id}>
              <td className="mono small"><b>{e.id}</b><div className="muted">{e.entryNo}</div></td>
              <td><JobLink id={e.jobId} /><div className="muted small">{e.customer}</div></td>
              <td>{e.entryType}<div className="muted small">{e.containers}</div></td>
              <td style={{ minWidth: 220 }}>
                <div className="row" style={{ gap: 3, flexWrap: 'nowrap' }}>{e.stages.map((s, i) => <span key={s} title={s} style={{ flex: 1, height: 6, borderRadius: 3, background: i <= e.stage ? 'var(--c)' : '#e6ecf2' }} />)}</div>
                <div className="small" style={{ marginTop: 4 }}>{e.stageName}</div>
              </td>
              <td className="small">{e.nextUnit === 'X' ? 'Customer collects' : UNIT_NAMES[e.nextUnit]}{e.handedOver && <div><Pill s="Handed over" kind="p-ok" /></div>}</td>
              <td>{e.stage < 5 && <button className="btn sm" disabled={adv.isPending} onClick={() => e.stage === 0 ? setLodge(e) : adv.mutate({ e })}>→ {e.stages[e.stage + 1]}</button>}</td>
            </tr>)}</tbody>
          </table></div>
        </section>;
      }}</Loading>
      {lodge && <LodgeModal e={lodge} busy={adv.isPending} onClose={() => setLodge(null)} onLodge={no => adv.mutate({ e: lodge, entryNo: no })} />}
    </>
  );
}

function LodgeModal({ e, busy, onClose, onLodge }: { e: Entry; busy: boolean; onClose: () => void; onLodge: (no: string) => void }) {
  const [no, setNo] = useState('');
  return (
    <Modal title={`Lodge ${e.entryType} entry · ${e.id}`} onClose={onClose}>
      <p className="muted" style={{ margin: 0 }}>Enter the TANSAD / customs entry number from TRA's system.</p>
      <label className="fl">Entry number<input className="inp mono" autoFocus placeholder="2026-DAR-121004" value={no} onChange={x => setNo(x.target.value)} /></label>
      <div className="actions"><button className="btn" onClick={onClose}>Cancel</button><button className="btn pri" disabled={!no.trim() || busy} onClick={() => onLodge(no)}>Lodge entry</button></div>
    </Modal>
  );
}

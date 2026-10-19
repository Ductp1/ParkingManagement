import React, { useState } from 'react';
import { AdminTitleBar } from '../../layout/AdminTitleBar';
import { AdminButton } from '../../components/AdminButton';
import { Banner } from '../../components/Banner';
import { KpiStrip, KpiTile } from '../../components/KpiTile';
import { Panel } from '../../components/Panel';
import { StateBlock } from '../../components/StateBlock';
import { ToolbarRow, ToolbarSelect } from '../../components/Toolbar';
import { FormDialog } from '../../components/FormDialog';
import { exportReportDialog } from '../unavailableScreens';
import type { ReportSpec } from './reportSpecs';

const ReportFilters: React.FC = () => (
  <ToolbarRow>
    {['Period', 'City', 'Lot'].map((label) => (
      <ToolbarSelect key={label} label={label} value="" width={190} options={[{ value: '', label: 'All' }]} onChange={() => undefined} disabled disabledReason="No report endpoint yet" />
    ))}
  </ToolbarRow>
);

/** Reports / {Revenue|Occupancy|Bookings|Owner performance} – chưa có API báo cáo → khung + trạng thái thật. */
export const ReportPage: React.FC<{ spec: ReportSpec }> = ({ spec }) => {
  const [exportOpen, setExportOpen] = useState(false);
  return (
    <>
      <AdminTitleBar title={spec.title} subtitle={spec.source} actions={<AdminButton variant="onNavy" icon="download" onClick={() => setExportOpen(true)}>Export CSV</AdminButton>} />
      <ReportFilters />
      <Banner tone="info">Data source: {spec.source}</Banner>
      {spec.kpis.length > 0 && (
        <KpiStrip>
          {spec.kpis.map((k) => (
            <KpiTile key={k} label={k} value="—" delta="No report endpoint yet" />
          ))}
        </KpiStrip>
      )}
      {spec.panels.map((p) => (
        <Panel key={p.title} title={p.title} description={p.description}>
          <StateBlock type="unavailable" bare message={`${spec.service} has no report endpoint yet. No chart is drawn instead of sample data.`} />
        </Panel>
      ))}
      {exportOpen && <FormDialog spec={exportReportDialog} open onClose={() => setExportOpen(false)} unavailableNote="No export endpoint yet." />}
    </>
  );
};

/** Reports / Exports – chưa có bảng export-job (Future trên Figma). */
export const ExportsPage: React.FC = () => {
  const [exportOpen, setExportOpen] = useState(false);
  return (
    <>
      <AdminTitleBar title="Exports" subtitle="CSV exports of report data" actions={<AdminButton variant="onNavy" icon="download" onClick={() => setExportOpen(true)}>Export CSV</AdminButton>} />
      <ReportFilters />
      <Banner tone="warning">Future: there is no export-job table. Exports are generated on demand; history is not stored.</Banner>
      <Panel title="Export history" description="Future · ExportJobs (not in DB)" future>
        <StateBlock type="unavailable" bare message="ExportJobs does not exist in any service database yet." />
      </Panel>
      {exportOpen && <FormDialog spec={exportReportDialog} open onClose={() => setExportOpen(false)} unavailableNote="No export endpoint yet." />}
    </>
  );
};

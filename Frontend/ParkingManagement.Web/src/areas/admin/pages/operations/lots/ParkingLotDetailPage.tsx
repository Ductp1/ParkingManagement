import React, { useState } from 'react';
import { useParams } from 'react-router-dom';
import { adminOperationsApi } from '@/api/adminOperationsApi';
import type { AdminParkingLotDto } from '@/types/adminOperations';
import { SanctionLevel } from '@/types/adminEnums';
import { AdminTitleBar } from '../../../layout/AdminTitleBar';
import { AdminButton } from '../../../components/AdminButton';
import { DetailColumns, QueryState, SummaryRow } from '../../../components/PageLayout';
import { Panel } from '../../../components/Panel';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { SubTabs } from '../../../components/SubTabs';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminBreadcrumb } from '../../../hooks/useAdminBreadcrumb';
import { formatNumber, orEmpty } from '../../../utils/format';
import { IssueSanctionDialog, type IssueSanctionPreset } from '../../support/sanctions/IssueSanctionDialog';
import { LotSanctionsPanel } from '../../support/sanctions/LotSanctionsPanel';
import { useLotSanctions } from '../../support/sanctions/useLotSanctions';
import { AmenitiesTab, CapacityTab, HoursTab, LayoutTab, PricingTab } from './LotDetailTabs';
import { isLotTabUnavailable, lotDetailSubTabs } from './lotDetailFormat';
import { ComingSoonState } from '../../../components/ComingSoonScreen';
import { NonPublicLotDetail } from './NonPublicLotDetail';

const InformationTab: React.FC<{ lot: AdminParkingLotDto; sanctionsPanel: React.ReactNode }> = ({ lot, sanctionsPanel }) => (
  <DetailColumns
    main={
      <>
        <Panel title="Lot information" description="Edited by the owner; read-only here">
          <DetailList>
            <DetailRow label="Name">{lot.name}</DetailRow>
            <DetailRow label="Address">{lot.address}</DetailRow>
            <DetailRow label="Coordinates">
              {lot.latitude.toFixed(4)}, {lot.longitude.toFixed(4)}
            </DetailRow>
            <DetailRow label="Hotline">{orEmpty(lot.hotlinePhone)}</DetailRow>
            <DetailRow label="Max vehicle height">{lot.maxHeightCm} cm</DetailRow>
            <DetailRow label="Default open / close">{lot.openingHours}</DetailRow>
            <DetailRow label="Description">{orEmpty(lot.description)}</DetailRow>
          </DetailList>
        </Panel>
        <Panel title="Capacity snapshot">
          <DetailList>
            <DetailRow label="Total slots">{formatNumber(lot.totalSlots)}</DetailRow>
            <DetailRow label="Available slots">{formatNumber(lot.availableSlots)}</DetailRow>
            <DetailRow label="Rating">
              {lot.ratingCount > 0 ? `${lot.ratingAverage.toLocaleString('vi-VN', { maximumFractionDigits: 1 })} ★ (${formatNumber(lot.ratingCount)} reviews)` : '—'}
            </DetailRow>
          </DetailList>
        </Panel>
      </>
    }
    side={sanctionsPanel}
  />
);

/** Operations / Parking Lots / Detail – các sub-tab có nguồn dữ liệu; tab chưa có hiện màn "not available yet". */
export const ParkingLotDetailPage: React.FC = () => {
  const { id, tab = '' } = useParams();
  const lotId = Number(id);
  const valid = Number.isInteger(lotId) && lotId > 0;
  const query = useAdminQuery(valid ? () => adminOperationsApi.getParkingLot(lotId) : null, [lotId]);
  const lot = query.data;
  useAdminBreadcrumb(lot ? lot.name : valid ? `Lot #${lotId}` : null);
  // US-097: chế tài của bãi + dialog áp chế tài (bãi và chủ bãi chọn sẵn).
  const sanctions = useLotSanctions(valid ? lotId : null);
  const [issuePreset, setIssuePreset] = useState<IssueSanctionPreset | null>(null);
  const ownerProfileId = sanctions.state.data?.ownerProfileId;
  const openIssue = (level?: SanctionLevel) =>
    ownerProfileId !== undefined && setIssuePreset({ ownerProfileId, lotIds: [lotId], level });

  if (!lot) {
    // Bãi chờ duyệt / bị tạm dừng: endpoint công khai trả 404 → dựng trang từ lots-summary (audit §7.6).
    if (valid && query.error?.status === 404) return <NonPublicLotDetail lotId={lotId} tab={tab} />;
    return (
      <>
        <AdminTitleBar title="Parking Lots" />
        <QueryState
          status={valid ? query.status : 'empty'}
          error={query.error}
          onRetry={query.refetch}
          bare={false}
          copy={{ emptyTitle: 'Parking lot not found', emptyMessage: 'Check the link.', errorTitle: 'Could not load the parking lot', service: 'ParkingService' }}
        />
      </>
    );
  }

  const base = `/admin/operations/parking-lots/${lot.id}`;
  return (
    <>
      <AdminTitleBar
        title={lot.name}
        subtitle={`Lot #${lot.id} · ${lot.address}`}
      />
      <SummaryRow
        items={[
          { label: 'Status', value: <StatusIndicator value={lot.status} /> },
          { label: 'Open now', value: <span className="adm-type-body-strong">{lot.isOpenNow ? 'Yes' : 'No'}</span> },
        ]}
        actions={
          <AdminButton
            variant="destructive"
            disabled={ownerProfileId === undefined}
            title={ownerProfileId === undefined ? 'Loading the lot owner…' : 'Issue an L3 listing suspension for this lot'}
            onClick={() => openIssue(SanctionLevel.TemporarySuspension)}
          >
            Suspend lot
          </AdminButton>
        }
      />
      <SubTabs tabs={lotDetailSubTabs(base)} />
      {tab === '' && (
        <InformationTab lot={lot} sanctionsPanel={<LotSanctionsPanel lotId={lot.id} sanctions={sanctions} onIssue={() => openIssue()} />} />
      )}
      {tab === 'layout' && <LayoutTab lotId={lot.id} />}
      {tab === 'capacity' && <CapacityTab lotId={lot.id} />}
      {tab === 'hours' && <HoursTab lotId={lot.id} />}
      {tab === 'amenities' && <AmenitiesTab lotId={lot.id} />}
      {tab === 'pricing' && <PricingTab lotId={lot.id} />}
      {isLotTabUnavailable(tab) && <ComingSoonState backTo={base} backLabel="Back to Information" />}
      {issuePreset && <IssueSanctionDialog preset={issuePreset} onClose={() => setIssuePreset(null)} onIssued={sanctions.reload} />}
    </>
  );
};

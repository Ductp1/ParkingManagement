import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { adminLotsApi } from '@/api/adminLotsApi';
import type { AdminLotSummaryRow, LotAttributesDto } from '@/types/adminLots';
import { AdminTitleBar } from '../../../layout/AdminTitleBar';
import { Banner } from '../../../components/Banner';
import { DetailColumns, SummaryRow } from '../../../components/PageLayout';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { Panel } from '../../../components/Panel';
import { StateBlock } from '../../../components/StateBlock';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { SubTabs } from '../../../components/SubTabs';
import { useAdminBreadcrumb } from '../../../hooks/useAdminBreadcrumb';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useLotOptions } from '../../../hooks/useLotOptions';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatNumber } from '../../../utils/format';
import { IssueSanctionDialog, type IssueSanctionPreset } from '../../support/sanctions/IssueSanctionDialog';
import { LotSanctionsPanel } from '../../support/sanctions/LotSanctionsPanel';
import { useLotSanctions } from '../../support/sanctions/useLotSanctions';
import { AmenitiesTab, CapacityTab, HoursTab, LayoutTab, PricingTab } from './LotDetailTabs';
import { LOT_DETAIL_MISSING, LOT_DETAIL_TABS, hiddenLotNotice } from './lotDetailFormat';

const LIST = '/admin/operations/parking-lots';

const area = (lot: AdminLotSummaryRow) => [lot.district, lot.city].filter(Boolean).join(', ');

/** Thông tin bãi từ lots-summary + attributes; chỉ hiện dòng có dữ liệu (không có địa chỉ / tọa độ cho bãi chưa công khai). */
const InformationTab: React.FC<{ lot: AdminLotSummaryRow; sanctionsPanel: React.ReactNode }> = ({ lot, sanctionsPanel }) => {
  const attributes = useAdminQuery<LotAttributesDto>(() => adminLotsApi.attributes(lot.parkingLotId), [lot.parkingLotId]);
  const a = attributes.data;
  return (
    <DetailColumns
      main={
        <Panel title="Lot information">
          <DetailList>
            <DetailRow label="Name">{lot.name}</DetailRow>
            <DetailRow label="City / district">{area(lot)}</DetailRow>
            <DetailRow label="Total slots">{formatNumber(lot.totalSlots)}</DetailRow>
            <DetailRow label="Integration">{humanizeEnum(String(lot.integrationTier))}</DetailRow>
            {a?.hotlinePhone && <DetailRow label="Hotline">{a.hotlinePhone}</DetailRow>}
            {a && a.maxHeightCm > 0 && <DetailRow label="Max vehicle height">{`${formatNumber(a.maxHeightCm)} cm`}</DetailRow>}
            {a?.description && <DetailRow label="Description">{a.description}</DetailRow>}
          </DetailList>
        </Panel>
      }
      side={sanctionsPanel}
    />
  );
};

/**
 * Lot detail cho bãi chưa công khai (chờ duyệt KYB / bị tạm dừng): GET /parking-lots/{id} cố ý trả 404 cho tài xế
 * (GetParkingLotByIdUseCase), nên trang dựng từ lots-summary (Admin) – các tab còn lại dùng endpoint riêng, chạy với mọi bãi.
 * Bãi không có trong lots-summary → "Parking lot not found" (thay cho thông báo "endpoint chưa có").
 */
export const NonPublicLotDetail: React.FC<{ lotId: number; tab: string }> = ({ lotId, tab }) => {
  const lots = useLotOptions();
  const lot = lots.byId(lotId);
  useAdminBreadcrumb(lot ? lot.name : null);
  const sanctions = useLotSanctions(lot ? lotId : null);
  const [issuePreset, setIssuePreset] = useState<IssueSanctionPreset | null>(null);
  const ownerProfileId = sanctions.state.data?.ownerProfileId;
  const openIssue = () => ownerProfileId !== undefined && setIssuePreset({ ownerProfileId, lotIds: [lotId] });

  if (!lot) {
    const status = lots.query.status;
    return (
      <>
        <AdminTitleBar title="Parking Lots" />
        {status === 'loading' ? (
          <StateBlock type="loading" />
        ) : status === 'ready' || status === 'empty' ? (
          <div className="flex flex-col items-center gap-3">
            <StateBlock type="empty" title="Parking lot not found" message="This lot does not exist or has been removed." className="w-full" />
            <Link to={LIST} className="adm-type-link-default text-(--admin-brand-navy-900) hover:underline">
              Back to parking lots
            </Link>
          </div>
        ) : status === 'forbidden' ? (
          <StateBlock type="noPermission" message="Only administrators can view this lot." />
        ) : (
          <StateBlock
            type="error"
            title="Could not load the parking lot"
            message={lots.query.error?.problem ? lots.query.error.message : 'The lot could not be loaded. Try again.'}
            onRetry={lots.query.refetch}
          />
        )}
      </>
    );
  }

  const base = `${LIST}/${lot.parkingLotId}`;
  return (
    <>
      <AdminTitleBar title={lot.name} subtitle={area(lot)} />
      <SummaryRow
        items={[
          { label: 'Status', value: <StatusIndicator value={String(lot.status)} /> },
          ...(lot.isEmergencyStopped ? [{ label: 'Flag', value: <StatusIndicator tone="danger" label="Emergency stopped" /> }] : []),
          ...(lot.instantBookingPaused ? [{ label: 'Flag', value: <StatusIndicator tone="warning" label="Instant booking paused" /> }] : []),
        ]}
      />
      <Banner tone="warning">{hiddenLotNotice(String(lot.status))}</Banner>
      <SubTabs tabs={LOT_DETAIL_TABS.map((t) => ({ label: t.label, to: t.key ? `${base}/${t.key}` : base }))} />
      {tab === '' && (
        <InformationTab lot={lot} sanctionsPanel={<LotSanctionsPanel lotId={lot.parkingLotId} sanctions={sanctions} onIssue={openIssue} />} />
      )}
      {tab === 'layout' && <LayoutTab lotId={lot.parkingLotId} />}
      {tab === 'capacity' && <CapacityTab lotId={lot.parkingLotId} />}
      {tab === 'hours' && <HoursTab lotId={lot.parkingLotId} />}
      {tab === 'amenities' && <AmenitiesTab lotId={lot.parkingLotId} />}
      {tab === 'pricing' && <PricingTab lotId={lot.parkingLotId} />}
      {LOT_DETAIL_MISSING[tab] && (
        <StateBlock
          type="unavailable"
          message={`ParkingService has no endpoint for ${LOT_DETAIL_TABS.find((t) => t.key === tab)?.label ?? tab} yet (${LOT_DETAIL_MISSING[tab]}).`}
        />
      )}
      {issuePreset && <IssueSanctionDialog preset={issuePreset} onClose={() => setIssuePreset(null)} onIssued={sanctions.reload} />}
    </>
  );
};

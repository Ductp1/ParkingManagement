import React, { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { adminKybApi } from '@/api/adminKybApi';
import { adminLotContractsApi } from '@/api/adminLotContractsApi';
import { KybStatus, LotContractStatus } from '@/types/adminEnums';
import type { AdminKybApplicationDetailDto } from '@/types/adminKyb';
import type { LotContractSummaryDto } from '@/types/adminLotContracts';
import { AdminTitleBar } from '../../../layout/AdminTitleBar';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { DetailColumns, QueryState, SummaryRow } from '../../../components/PageLayout';
import { Panel } from '../../../components/Panel';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { Timeline, type TimelineEntry } from '../../../components/Timeline';
import { AdminIcon } from '../../../components/AdminIcon';
import { ADMIN_ICONS } from '../../../components/adminIcons';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminBreadcrumb } from '../../../hooks/useAdminBreadcrumb';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { formatDateTimeIct, formatNumber, orEmpty } from '../../../utils/format';
import { cityText, fileNameFromUrl, kybCode, ownerLabel, shortTime } from './kybFormat';
import { KybDialogs, type KybDialogType } from './KybDialogs';
import { AuditTrailLink } from '../../../components/AuditTrailLink';
import { auditLogListLink } from '../../access/auditLogFormat';
import { LotContractPanel } from '../lot-contracts/LotContractPanel';

const adminLabel = (id: number | null) => (id ? `Admin #${id}` : '—');

const buildTimeline = (kyb: AdminKybApplicationDetailDto): TimelineEntry[] => {
  const s = kyb.status;
  const reviewed = `${formatDateTimeIct(kyb.reviewedAtUtc)} · ${adminLabel(kyb.reviewedByUserId)}`;
  const entries: TimelineEntry[] = [
    { key: 'draft', title: 'Draft created', meta: `${formatDateTimeIct(kyb.createdAtUtc)} · Owner`, state: s === KybStatus.Draft ? 'current' : 'done' },
    {
      key: 'submitted',
      title: 'Submitted',
      meta: formatDateTimeIct(kyb.submittedAtUtc),
      state: s === KybStatus.Draft ? 'upcoming' : s === KybStatus.Submitted ? 'current' : 'done',
    },
  ];
  if (kyb.fieldSurveyAtUtc) {
    entries.push({
      key: 'survey',
      title: kyb.fieldSurveyCompletedAtUtc ? 'Field survey done' : 'Field survey scheduled',
      meta: formatDateTimeIct(kyb.fieldSurveyCompletedAtUtc ?? kyb.fieldSurveyAtUtc),
      state: kyb.fieldSurveyCompletedAtUtc ? 'done' : s === KybStatus.UnderReview ? 'current' : 'upcoming',
    });
  }
  if (s === KybStatus.Draft || s === KybStatus.Submitted) {
    entries.push({ key: 'review', title: 'Under review', state: 'upcoming' }, { key: 'approved', title: 'Approved', state: 'upcoming' });
  } else if (s === KybStatus.UnderReview) {
    entries.push(
      { key: 'review', title: 'Under review', meta: adminLabel(kyb.reviewedByUserId), state: 'current' },
      { key: 'approved', title: 'Approved', state: 'upcoming' }
    );
  } else if (s === KybStatus.Approved) {
    entries.push({ key: 'approved', title: 'Approved', meta: reviewed, state: 'done' });
  } else if (s === KybStatus.Rejected) {
    entries.push({ key: 'rejected', title: 'Rejected', meta: reviewed, state: 'danger' });
  } else if (s === KybStatus.NeedsMoreInfo) {
    entries.push({ key: 'info', title: 'Needs more info', meta: reviewed, state: 'current' });
  }
  return entries;
};

const DocLink: React.FC<{ url: string | null }> = ({ url }) =>
  url ? (
    <a href={url} target="_blank" rel="noreferrer" className="adm-type-link-default inline-flex items-center gap-1 text-(--admin-brand-navy-900) hover:underline">
      {fileNameFromUrl(url)}
      <AdminIcon icon={ADMIN_ICONS.external} />
    </a>
  ) : (
    <>—</>
  );

const KybDetailBody: React.FC<{
  kyb: AdminKybApplicationDetailDto;
  /** Hợp đồng chưa hủy của bãi (US-116), null nếu chưa lập. */
  lotContract: LotContractSummaryDto | null;
  onAction: (t: KybDialogType) => void;
  /** Gọi sau thao tác hợp đồng (ký hợp đồng = duyệt KYB nên phải tải lại hồ sơ). */
  onContractChanged: () => void;
}> = ({ kyb, lotContract, onAction, onContractChanged }) => {
  const navigate = useNavigate();
  const lot = kyb.parkingLot;
  const lotPath = `/admin/operations/parking-lots/${lot.id}`;
  const hasGps = kyb.photoLatitude !== null && kyb.photoLongitude !== null;
  const underReview = kyb.status === KybStatus.UnderReview;
  const surveyDue = underReview && kyb.fieldSurveyAtUtc !== null && kyb.fieldSurveyCompletedAtUtc === null;
  const contractPending = lotContract?.status === LotContractStatus.Pending;
  // Điều kiện duyệt của ParkingService + hợp đồng bãi đã ký (design v9: tải bản đã ký = duyệt).
  const blockers = underReview && !contractPending ? [...kyb.approvalBlockers, 'No lot contract is waiting for signature.'] : kyb.approvalBlockers;

  const titleActions =
    kyb.status === KybStatus.Submitted ? (
      <AdminButton onClick={() => onAction('startReview')}>Start review</AdminButton>
    ) : underReview ? (
      <>
        <AdminButton variant="onNavy" onClick={() => onAction('requestInfo')}>
          Request more info
        </AdminButton>
      </>
    ) : kyb.status === KybStatus.Approved ? (
      <AdminButton variant="onNavy" onClick={() => navigate(lotPath)}>
        Open lot
      </AdminButton>
    ) : undefined;

  return (
    <>
      <AdminTitleBar
        title={lot.name}
        subtitle={`${kybCode(kyb.id)} · Lot #${lot.id} · submitted ${formatDateTimeIct(kyb.submittedAtUtc)}`}
        actions={
          <>
            <AuditTrailLink to={auditLogListLink({ entity: 'KybApplication', entityId: String(kyb.id) })} />
            {titleActions}
          </>
        }
      />

      {underReview && blockers.length > 0 && (
        <Banner tone="warning">Not ready to approve: {blockers.join(' ')} Approve by uploading the signed lot contract below.</Banner>
      )}
      {kyb.status === KybStatus.NeedsMoreInfo && (
        <Banner tone="warning">
          More info requested {formatDateTimeIct(kyb.reviewedAtUtc)} by {adminLabel(kyb.reviewedByUserId)}: {orEmpty(kyb.reviewNote)} · the owner can edit
          and resubmit.
        </Banner>
      )}
      {kyb.status === KybStatus.Approved && (
        <Banner tone="success">
          Approved {formatDateTimeIct(kyb.reviewedAtUtc)} by {adminLabel(kyb.reviewedByUserId)}
          {lotContract ? ` · lot contract ${lotContract.contractNumber} signed` : ''} · lot status {lot.status}.
          {kyb.reviewNote ? ` Note: ${kyb.reviewNote}` : ''}
        </Banner>
      )}
      {kyb.status === KybStatus.Rejected && (
        <Banner tone="danger">
          Rejected {formatDateTimeIct(kyb.reviewedAtUtc)} by {adminLabel(kyb.reviewedByUserId)} · RejectReason: {orEmpty(kyb.rejectReason)}
        </Banner>
      )}

      <SummaryRow
        items={[
          { label: 'KYB status', value: <StatusIndicator value={kyb.status} /> },
          { label: 'Lot status', value: <StatusIndicator value={lot.status} /> },
        ]}
        actions={
          underReview && (
            <>
              {surveyDue ? (
                <AdminButton variant="secondary" icon="calendar" onClick={() => onAction('recordSurvey')}>
                  Record survey result
                </AdminButton>
              ) : (
                <AdminButton variant="secondary" icon="calendar" onClick={() => onAction('scheduleSurvey')}>
                  {kyb.fieldSurveyAtUtc ? 'Reschedule field survey' : 'Schedule field survey'}
                </AdminButton>
              )}
              <AdminButton variant="destructive" onClick={() => onAction('reject')}>
                Reject
              </AdminButton>
            </>
          )
        }
      />

      <DetailColumns
        main={
          <>
            {kyb.status !== KybStatus.Draft && <LotContractPanel kyb={kyb} summary={lotContract} onChanged={onContractChanged} />}

            <Panel title="Documents" description="KybApplications.BusinessLicenseUrl · FireSafetyCertificateUrl · SitePhotoUrlsJson">
              <DetailList>
                <DetailRow label="Business licence">
                  <DocLink url={kyb.businessLicenseUrl} />
                </DetailRow>
                <DetailRow label="Fire safety certificate">
                  <DocLink url={kyb.fireSafetyCertificateUrl} />
                </DetailRow>
                <DetailRow label="Site photos">{kyb.sitePhotoUrls.length > 0 ? `${kyb.sitePhotoUrls.length} photos` : '—'}</DetailRow>
              </DetailList>
              {kyb.sitePhotoUrls.length > 0 && (
                <div className="mt-6 flex flex-wrap gap-3">
                  {kyb.sitePhotoUrls.map((url) => (
                    <a key={url} href={url} target="_blank" rel="noreferrer" className="flex w-[150px] flex-col gap-1">
                      <img src={url} alt={fileNameFromUrl(url)} className="h-24 w-[150px] border border-(--admin-border-default) bg-(--admin-surface-selected) object-cover" />
                      <span className="adm-type-body-small truncate text-(--admin-text-secondary)">{fileNameFromUrl(url)}</span>
                    </a>
                  ))}
                </div>
              )}
            </Panel>

            <Panel title="Location check" description="PhotoLatitude / PhotoLongitude vs ParkingLots.Latitude / Longitude (warning above 100 m)">
              <DetailList>
                <DetailRow label="Photo coordinates">{hasGps ? `${kyb.photoLatitude!.toFixed(4)}, ${kyb.photoLongitude!.toFixed(4)}` : '—'}</DetailRow>
                <DetailRow label="Lot coordinates">{`${lot.latitude.toFixed(4)}, ${lot.longitude.toFixed(4)}`}</DetailRow>
                <DetailRow label="Distance">
                  {kyb.gpsDistanceMeters !== null ? `${formatNumber(Math.round(kyb.gpsDistanceMeters))} m` : '—'}
                  {kyb.gpsWarning && (
                    <>
                      {' '}
                      <StatusIndicator tone="warning" label={hasGps ? 'Check location' : 'No GPS on photos'} />
                    </>
                  )}
                </DetailRow>
              </DetailList>
            </Panel>

            <Panel title="Field survey" description="FieldSurveyRequired · FieldSurveyAtUtc · FieldSurveyCompletedAtUtc · FieldSurveyNote">
              <DetailList>
                <DetailRow label="Required">
                  {kyb.fieldSurveyRequired ? 'Yes' : 'No'}
                  {kyb.fieldSurveyMandatory ? ' · mandatory (over 50 slots)' : ''}
                </DetailRow>
                <DetailRow label="Scheduled at">{formatDateTimeIct(kyb.fieldSurveyAtUtc)}</DetailRow>
                <DetailRow label="Completed at">{formatDateTimeIct(kyb.fieldSurveyCompletedAtUtc)}</DetailRow>
                <DetailRow label="Note">{orEmpty(kyb.fieldSurveyNote)}</DetailRow>
              </DetailList>
            </Panel>

            <Panel title="Per-check results" description="Not stored — only the overall Status exists">
              <DetailList>
                {['Legal documents', 'Location & GPS', 'Fire safety', 'Field survey'].map((label) => (
                  <DetailRow key={label} label={label} future>
                    —
                  </DetailRow>
                ))}
              </DetailList>
            </Panel>
          </>
        }
        side={
          <>
            <Panel title="Lot & owner" action={{ label: 'Open lot →', to: lotPath }}>
              <DetailList>
                <DetailRow label="Lot">{lot.name}</DetailRow>
                <DetailRow label="Address">{lot.address}</DetailRow>
                <DetailRow label="City">{cityText(lot.city, lot.district)}</DetailRow>
                <DetailRow label="Total slots">{formatNumber(lot.totalSlots)}</DetailRow>
                <DetailRow label="Active slots">{formatNumber(lot.activeSlotCount)}</DetailRow>
                <DetailRow label="Max height">{`${formatNumber(lot.maxHeightCm)} cm`}</DetailRow>
                <DetailRow label="Opening hours">{`${shortTime(lot.openTime)} – ${shortTime(lot.closeTime)}`}</DetailRow>
                <DetailRow label="Lot status">
                  <StatusIndicator value={lot.status} />
                </DetailRow>
                <DetailRow label="Owner">{ownerLabel(lot.ownerProfileId)}</DetailRow>
              </DetailList>
            </Panel>
            <Panel title="Status timeline" description="CreatedAtUtc · SubmittedAtUtc · FieldSurvey · ReviewedAtUtc">
              <Timeline entries={buildTimeline(kyb)} />
            </Panel>
          </>
        }
      />
    </>
  );
};

/** Operations / KYB Review / Detail (US-055 + US-116 v9): 4 phần hồ sơ, hợp đồng bãi (bàn bạc + ký = duyệt) và các thao tác chuyển trạng thái. */
export const KybDetailPage: React.FC = () => {
  const { id } = useParams();
  const kybId = Number(id);
  const toast = useAdminToast();
  const [dialog, setDialog] = useState<KybDialogType | null>(null);

  const query = useAdminQuery(Number.isInteger(kybId) && kybId > 0 ? () => adminKybApi.getById(kybId) : null, [kybId]);
  const lotId = query.data?.parkingLot.id ?? null;
  const contracts = useAdminQuery(lotId ? () => adminLotContractsApi.list({ parkingLotId: lotId, page: 1, pageSize: 10 }) : null, [lotId]);
  const lotContract = contracts.data?.items.find((c) => c.status !== LotContractStatus.Cancelled) ?? null;

  useAdminBreadcrumb(Number.isInteger(kybId) ? kybCode(kybId) : null);

  if (!query.data) {
    return (
      <>
        <AdminTitleBar title="KYB Review" subtitle="—" />
        <QueryState
          status={Number.isInteger(kybId) && kybId > 0 ? query.status : 'empty'}
          error={query.error}
          onRetry={query.refetch}
          bare={false}
          copy={{
            emptyTitle: 'KYB application not found',
            emptyMessage: 'Check the link or go back to the KYB Review list.',
            errorTitle: 'Could not load the KYB application',
            service: 'ParkingService',
          }}
        />
      </>
    );
  }

  return (
    <>
      <KybDetailBody
        kyb={query.data}
        lotContract={lotContract}
        onAction={setDialog}
        onContractChanged={() => {
          query.refetch();
          contracts.refetch();
        }}
      />
      <KybDialogs
        kyb={query.data}
        open={dialog}
        onClose={() => setDialog(null)}
        onDone={(title, message) => {
          toast.show({ type: 'success', title, message });
          query.refetch();
        }}
      />
    </>
  );
};

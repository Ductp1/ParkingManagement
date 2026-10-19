import React, { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { adminKybApi } from '@/api/adminKybApi';
import { adminOperationsApi } from '@/api/adminOperationsApi';
import { KybStatus } from '@/types/adminEnums';
import type { AdminKybApplicationDetailDto } from '@/types/adminKyb';
import type { AdminParkingLotDto } from '@/types/adminOperations';
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
import { distanceMeters } from '../../../utils/geo';
import { formatDateTimeIct, formatNumber, orEmpty } from '../../../utils/format';
import { fileNameFromUrl, kybCode } from './kybFormat';
import { KybDialogs, type KybDialogType } from './KybDialogs';

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

const KybDetailBody: React.FC<{ kyb: AdminKybApplicationDetailDto; lot: AdminParkingLotDto | null; onAction: (t: KybDialogType) => void }> = ({
  kyb,
  lot,
  onAction,
}) => {
  const navigate = useNavigate();
  const lotPath = `/admin/operations/parking-lots/${kyb.parkingLotId}`;
  const hasGps = kyb.photoLatitude !== null && kyb.photoLongitude !== null;
  const distance = hasGps && lot ? Math.round(distanceMeters(kyb.photoLatitude!, kyb.photoLongitude!, lot.latitude, lot.longitude)) : null;

  const titleActions =
    kyb.status === KybStatus.Submitted ? (
      <AdminButton onClick={() => onAction('startReview')}>Start review</AdminButton>
    ) : kyb.status === KybStatus.UnderReview ? (
      <>
        <AdminButton variant="onNavy" onClick={() => onAction('requestInfo')}>
          Request more info
        </AdminButton>
        <AdminButton onClick={() => onAction('approve')}>Approve</AdminButton>
      </>
    ) : kyb.status === KybStatus.Approved ? (
      <AdminButton variant="onNavy" onClick={() => navigate(lotPath)}>
        Open lot
      </AdminButton>
    ) : undefined;

  return (
    <>
      <AdminTitleBar
        title={kyb.parkingLotName}
        subtitle={`${kybCode(kyb.id)} · Lot #${kyb.parkingLotId} · submitted ${formatDateTimeIct(kyb.submittedAtUtc)}`}
        actions={titleActions}
      />

      {kyb.status === KybStatus.NeedsMoreInfo && (
        <Banner tone="warning">
          More info requested {formatDateTimeIct(kyb.reviewedAtUtc)} by {adminLabel(kyb.reviewedByUserId)} · the owner can edit and resubmit.
        </Banner>
      )}
      {kyb.status === KybStatus.Approved && (
        <Banner tone="success">
          Approved {formatDateTimeIct(kyb.reviewedAtUtc)} by {adminLabel(kyb.reviewedByUserId)} · lot status changed Pending approval → Active.
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
          { label: 'Lot status', value: lot ? <StatusIndicator value={lot.status} /> : <span className="adm-type-body-strong">—</span> },
        ]}
        actions={
          kyb.status === KybStatus.UnderReview && (
            <>
              <AdminButton variant="secondary" icon="calendar" onClick={() => onAction('scheduleSurvey')}>
                Schedule field survey
              </AdminButton>
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

            <Panel title="Location check" description="PhotoLatitude / PhotoLongitude vs ParkingLots.Latitude / Longitude">
              <DetailList>
                <DetailRow label="Photo coordinates">{hasGps ? `${kyb.photoLatitude!.toFixed(4)}, ${kyb.photoLongitude!.toFixed(4)}` : '—'}</DetailRow>
                <DetailRow label="Lot coordinates">{lot ? `${lot.latitude.toFixed(4)}, ${lot.longitude.toFixed(4)}` : '—'}</DetailRow>
                <DetailRow label="Distance">{distance !== null ? `${formatNumber(distance)} m` : '—'}</DetailRow>
              </DetailList>
            </Panel>

            <Panel title="Field survey" description="FieldSurveyRequired · FieldSurveyAtUtc · FieldSurveyNote">
              <DetailList>
                <DetailRow label="Required">{kyb.fieldSurveyRequired ? 'Yes' : 'No'}</DetailRow>
                <DetailRow label="Scheduled / done at">{formatDateTimeIct(kyb.fieldSurveyAtUtc)}</DetailRow>
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
                <DetailRow label="Lot">{kyb.parkingLotName}</DetailRow>
                <DetailRow label="Address">{kyb.parkingLotAddress}</DetailRow>
                <DetailRow label="Total slots">{lot ? formatNumber(lot.totalSlots) : '—'}</DetailRow>
                <DetailRow label="Lot status">{lot ? <StatusIndicator value={lot.status} /> : '—'}</DetailRow>
                <DetailRow label="OwnerProfile">
                  #{kyb.ownerProfileId}
                  {kyb.ownerBusinessName ? ` · ${kyb.ownerBusinessName}` : ''}
                </DetailRow>
              </DetailList>
            </Panel>
            <Panel title="Status timeline" description="CreatedAtUtc · SubmittedAtUtc · ReviewedAtUtc">
              <Timeline entries={buildTimeline(kyb)} />
            </Panel>
          </>
        }
      />
    </>
  );
};

/** Operations / KYB Review / Detail (US-055): 4 phần hồ sơ + các thao tác chuyển trạng thái. */
export const KybDetailPage: React.FC = () => {
  const { id } = useParams();
  const kybId = Number(id);
  const toast = useAdminToast();
  const [dialog, setDialog] = useState<KybDialogType | null>(null);

  const query = useAdminQuery(Number.isInteger(kybId) && kybId > 0 ? () => adminKybApi.getById(kybId) : null, [kybId]);
  const lotId = query.data?.parkingLotId ?? null;
  const lotQuery = useAdminQuery(lotId ? () => adminOperationsApi.getParkingLot(lotId) : null, [lotId]);

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
      <KybDetailBody kyb={query.data} lot={lotQuery.data} onAction={setDialog} />
      <KybDialogs
        kyb={query.data}
        lot={lotQuery.data}
        open={dialog}
        onClose={() => setDialog(null)}
        onDone={(title, message) => {
          toast.show({ type: 'success', title, message });
          query.refetch();
          lotQuery.refetch();
        }}
      />
    </>
  );
};
